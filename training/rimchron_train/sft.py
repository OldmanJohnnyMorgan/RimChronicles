"""`sft`: QLoRA supervised fine-tuning with Unsloth + TRL (training spec §5.2), loss on the answer only."""
from __future__ import annotations

import inspect
import json
import subprocess
from pathlib import Path

from . import data
from .config import Config


def _git_commit() -> str:
    try:
        return subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
    except Exception:  # noqa: BLE001
        return "unknown"


def load_model(cfg: Config):
    from unsloth import FastModel  # FastModel covers Gemma's multimodal checkpoints (text-only use here)

    model, tokenizer = FastModel.from_pretrained(
        model_name=cfg.base_model, max_seq_length=cfg.max_seq_length, load_in_4bit=cfg.load_in_4bit, full_finetuning=False)
    return model, tokenizer


def render(tokenizer, row: data.Row, with_answer: bool = True) -> str:
    text = tokenizer.apply_chat_template(row.messages(with_answer), tokenize=False, add_generation_prompt=not with_answer)
    return text.removeprefix(getattr(tokenizer, "bos_token", None) or "")  # the trainer adds <bos> itself


def check_markers(cfg: Config, tokenizer, row: data.Row) -> None:
    text = render(tokenizer, row)
    for name, marker in (("instruction_marker", cfg.instruction_marker), ("response_marker", cfg.response_marker)):
        if marker not in text:
            raise SystemExit(f"{name} {marker!r} is not in the rendered chat template. Rendered example:\n{text[:600]}\n"
                             f"Set {name} in the config to this model's turn markers.")


def run(cfg: Config) -> int:
    import torch
    from datasets import Dataset
    from trl import SFTConfig, SFTTrainer
    from unsloth import FastModel
    from unsloth.chat_templates import train_on_responses_only

    manifest = data.check_manifest(cfg.dataset)
    train_rows = data.load_split(cfg.dataset, "train")
    val_rows = data.load_split(cfg.dataset, "val")
    print(f"dataset {cfg.dataset}: {len(train_rows)} train / {len(val_rows)} val rows, prompt {manifest['promptVersion']}, tier {manifest['tier']}")

    free, total = torch.cuda.mem_get_info(0)
    print(f"VRAM {free / 2**30:.1f} of {total / 2**30:.1f} GB free")
    model, tokenizer = load_model(cfg)
    check_markers(cfg, tokenizer, train_rows[0])
    model = FastModel.get_peft_model(
        model, finetune_vision_layers=False, finetune_language_layers=True, finetune_attention_modules=True,
        finetune_mlp_modules=True, r=cfg.lora_r, lora_alpha=cfg.lora_alpha, lora_dropout=cfg.lora_dropout,
        bias="none", random_state=cfg.seed)

    train = Dataset.from_list([{"text": render(tokenizer, r)} for r in train_rows])
    # TRL renamed max_seq_length -> max_length and tokenizer -> processing_class; use whichever this version takes.
    length_key = "max_seq_length" if "max_seq_length" in inspect.signature(SFTConfig.__init__).parameters else "max_length"
    args = SFTConfig(
        dataset_text_field="text", **{length_key: cfg.max_seq_length},
        per_device_train_batch_size=cfg.per_device_batch, gradient_accumulation_steps=cfg.grad_accumulation,
        learning_rate=cfg.learning_rate, lr_scheduler_type="cosine", warmup_ratio=cfg.warmup_ratio,
        num_train_epochs=cfg.epochs, max_steps=cfg.max_steps, logging_steps=1, optim="adamw_8bit",
        seed=cfg.seed, output_dir=str(cfg.run_dir / "checkpoints"), report_to="none")
    tokenizer_key = "processing_class" if "processing_class" in inspect.signature(SFTTrainer.__init__).parameters else "tokenizer"
    trainer = SFTTrainer(model=model, train_dataset=train, args=args, **{tokenizer_key: tokenizer})
    trainer = train_on_responses_only(trainer, instruction_part=cfg.instruction_marker, response_part=cfg.response_marker)
    stats = trainer.train()

    cfg.run_dir.mkdir(parents=True, exist_ok=True)
    model.save_pretrained(str(cfg.run_dir / "lora"))
    tokenizer.save_pretrained(str(cfg.run_dir / "lora"))

    valid = evaluate(model, tokenizer, val_rows[:20])
    manifest_out = {
        "run": cfg.run_name, "gitCommit": _git_commit(), "baseModel": cfg.base_model, "dataset": str(cfg.dataset),
        "datasetManifest": manifest, "config": {k: str(v) for k, v in vars(cfg).items()},
        "trainLoss": stats.training_loss, "valValidJson": valid,
    }
    (cfg.run_dir / "manifest.json").write_text(json.dumps(manifest_out, indent=2), encoding="utf-8")
    print(f"saved the LoRA to {cfg.run_dir / 'lora'}; val answers valid {valid['valid']}/{valid['total']}, "
          f"matching the teacher {valid['agree']}/{valid['total']}")
    return 0


def evaluate(model, tokenizer, rows: list[data.Row]) -> dict:
    """Generates answers without grammar: valid JSON with an allowed choice, and agreement with the teacher."""
    from unsloth import FastModel

    FastModel.for_inference(model)
    valid = agree = 0
    for row in rows:
        prompt = render(tokenizer, row, with_answer=False)
        inputs = tokenizer(text=prompt, return_tensors="pt").to(model.device)
        out = model.generate(**inputs, max_new_tokens=200, do_sample=False)
        text = tokenizer.decode(out[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True)
        ok = data.is_valid_answer(text, row.allowed)
        valid += ok
        agree += ok and json.loads(text[text.index("{"):text.rindex("}") + 1])["choice"] == row.choice()
    return {"total": len(rows), "valid": valid, "agree": agree}
