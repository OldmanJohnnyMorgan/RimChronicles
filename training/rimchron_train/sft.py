"""`sft`: QLoRA supervised fine-tuning with Unsloth + TRL (training spec §5.2), loss on the answer only."""
from __future__ import annotations

import inspect
import json
import subprocess
from pathlib import Path

from . import data, progress
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


def check_lengths(lengths: list[int], limit: int, ids: list[str]) -> None:
    """Stops if any row is longer than max_seq_length: the trainer cuts rows from the end, and the end is the answer,
    the only part with loss, so a long row would train on nothing (or half a JSON) without a word."""
    over = [(i, n) for i, n in zip(ids, lengths) if n > limit]
    if over:
        worst = max(over, key=lambda x: x[1])
        raise SystemExit(f"{len(over)} training rows are longer than max_seq_length {limit} tokens "
                         f"(longest: {worst[0]}, {worst[1]} tokens). Raise max_seq_length in the config, "
                         f"or rebuild the dataset for a smaller tier.")


def run(cfg: Config) -> int:
    # Unsloth first: it patches trl, transformers and peft when imported, and warns (and runs slower) otherwise.
    from unsloth import FastModel
    from unsloth.chat_templates import train_on_responses_only

    import time

    import torch
    from datasets import Dataset
    from transformers import TrainerCallback
    from trl import SFTConfig, SFTTrainer

    manifest = data.check_manifest(cfg.dataset)
    train_rows = data.load_split(cfg.dataset, "train")
    val_rows = data.load_split(cfg.dataset, "val")
    eval_rows = progress.eval_sample(val_rows, cfg.eval_rows)
    print(f"dataset {cfg.dataset}: {len(train_rows)} train / {len(val_rows)} val rows, prompt {manifest['promptVersion']}, "
          f"tier {manifest['tier']}; generating on {len(eval_rows)} val cases every {cfg.eval_every_steps or 'no'} steps")

    free, total = torch.cuda.mem_get_info(0)
    print(f"VRAM {free / 2**30:.1f} of {total / 2**30:.1f} GB free")
    if free / 2**30 < cfg.min_free_vram_gb:
        raise SystemExit(f"Only {free / 2**30:.1f} GB of VRAM is free; this run needs about {cfg.min_free_vram_gb} GB. "
                         "Close RimWorld, Ollama models (`ollama stop <model>`) and other training jobs, then try again.")
    model, tokenizer = load_model(cfg)
    check_markers(cfg, tokenizer, train_rows[0])
    model = FastModel.get_peft_model(
        model, finetune_vision_layers=False, finetune_language_layers=True, finetune_attention_modules=True,
        finetune_mlp_modules=True, r=cfg.lora_r, lora_alpha=cfg.lora_alpha, lora_dropout=cfg.lora_dropout,
        bias="none", random_state=cfg.seed, use_gradient_checkpointing="unsloth")

    texts = [render(tokenizer, r) for r in train_rows]
    lengths = [len(tokenizer(text=t)["input_ids"]) for t in texts]
    check_lengths(lengths, cfg.max_seq_length, [r.id for r in train_rows])
    print(progress.length_report(lengths, cfg.max_seq_length))
    train = Dataset.from_list([{"text": t} for t in texts])
    # TRL renamed max_seq_length -> max_length and tokenizer -> processing_class; use whichever this version takes.
    length_key = "max_seq_length" if "max_seq_length" in inspect.signature(SFTConfig.__init__).parameters else "max_length"
    args = SFTConfig(
        dataset_text_field="text", **{length_key: cfg.max_seq_length},
        per_device_train_batch_size=cfg.per_device_batch, gradient_accumulation_steps=cfg.grad_accumulation,
        learning_rate=cfg.learning_rate, lr_scheduler_type="cosine", warmup_ratio=cfg.warmup_ratio,
        num_train_epochs=cfg.epochs, max_steps=cfg.max_steps, logging_steps=10, optim="adamw_8bit",
        seed=cfg.seed, output_dir=str(cfg.run_dir / "checkpoints"), save_strategy="no", report_to="none")
    tokenizer_key = "processing_class" if "processing_class" in inspect.signature(SFTTrainer.__init__).parameters else "tokenizer"
    trainer = SFTTrainer(model=model, train_dataset=train, args=args, **{tokenizer_key: tokenizer})
    trainer = train_on_responses_only(trainer, instruction_part=cfg.instruction_marker, response_part=cfg.response_marker)

    if moved := progress.set_aside(cfg.run_dir):
        print(f"an earlier run of {cfg.run_name} was moved to {moved}")
    lora_dir = cfg.run_dir / "lora"
    log = progress.CsvLog(cfg.run_dir / "progress.csv")
    stopper = progress.EarlyStopper(cfg.patience, cfg.min_delta)
    started = time.time()
    evals: dict[int, dict] = {}

    def save_lora() -> None:
        model.save_pretrained(str(lora_dir))
        tokenizer.save_pretrained(str(lora_dir))

    def check(state) -> bool:
        """Generates on the val sample, logs it, keeps the best LoRA; returns True to stop training."""
        result = evaluate(model, tokenizer, eval_rows, cfg.max_new_tokens)
        back_to_training(model)
        improved, stop = stopper.update(state.global_step, result.top1)
        if improved:
            save_lora()
        evals[state.global_step] = result.as_dict()
        loss = progress.last_train_loss(state.log_history)
        log.write(step=state.global_step, epoch=round(state.epoch or 0, 2), train_loss=loss, val_valid=round(result.valid_share, 4),
                  val_top1=round(result.top1, 4), val_wait=round(result.wait_share, 4), best_top1=round(stopper.best, 4),
                  best_step=stopper.best_step, elapsed_min=round((time.time() - started) / 60, 1))
        print(f"step {state.global_step} (epoch {state.epoch or 0:.2f}): loss {loss}, val valid {result.valid}/{result.total}, "
              f"top-1 {result.agree}/{result.total} ({result.top1:.0%}), Wait {result.wait_share:.0%}; "
              f"best {stopper.best:.0%} at step {stopper.best_step}{' (saved)' if improved else ''}{'; stopping early' if stop else ''}")
        return stop

    class GenerateEval(TrainerCallback):
        def on_step_end(self, args, state, control, **kwargs):
            if cfg.eval_every_steps and state.global_step % cfg.eval_every_steps == 0 and check(state):
                control.should_training_stop = True
            return control

    trainer.add_callback(GenerateEval())
    stats = trainer.train()
    final_step = trainer.state.global_step
    if final_step not in evals:
        check(trainer.state)  # the end of training is always evaluated, and kept if it's the best
    if stopper.best_step is None:
        save_lora()

    manifest_out = {
        "run": cfg.run_name, "gitCommit": _git_commit(), "baseModel": cfg.base_model, "dataset": str(cfg.dataset),
        "datasetManifest": manifest, "config": {k: str(v) for k, v in vars(cfg).items()}, "versions": _versions(),
        "trainLoss": stats.training_loss, "steps": final_step, "minutes": round((time.time() - started) / 60, 1),
        "bestStep": stopper.best_step, "bestVal": evals.get(stopper.best_step), "evals": evals,
    }
    (cfg.run_dir / "manifest.json").write_text(json.dumps(manifest_out, indent=2), encoding="utf-8")
    best = evals.get(stopper.best_step) or {}
    print(f"saved the best LoRA (step {stopper.best_step}) to {lora_dir}; val answers valid {best.get('valid')}/{best.get('total')}, "
          f"matching the teacher {best.get('agree')}/{best.get('total')}. Log: {cfg.run_dir / 'progress.csv'}")
    return 0


def back_to_training(model) -> None:
    """Undo `for_inference` after an evaluation (Unsloth switches kernels and caching for generation)."""
    from unsloth import FastModel

    if hasattr(FastModel, "for_training"):
        # Keep the offloaded checkpointing get_peft_model chose: the default could switch it back and raise VRAM use.
        if "use_gradient_checkpointing" in inspect.signature(FastModel.for_training).parameters:
            FastModel.for_training(model, use_gradient_checkpointing="unsloth")
        else:
            FastModel.for_training(model)
    else:
        model.train()


def _versions() -> dict:
    import importlib.metadata as md

    out = {}
    for pkg in ("torch", "transformers", "trl", "peft", "unsloth", "datasets"):
        try:
            out[pkg] = md.version(pkg)
        except md.PackageNotFoundError:
            out[pkg] = None
    return out


def evaluate(model, tokenizer, rows: list[data.Row], max_new_tokens: int = 160) -> progress.EvalResult:
    """Generates answers greedily, without grammar (stricter than the game, which constrains decoding), and scores
    them against the teacher: valid JSON with an allowed choice, top-1 agreement, and how often it picks Wait."""
    from unsloth import FastModel

    FastModel.for_inference(model)
    answers = []
    for row in rows:
        prompt = render(tokenizer, row, with_answer=False)
        inputs = tokenizer(text=prompt, return_tensors="pt").to(model.device)
        out = model.generate(**inputs, max_new_tokens=max_new_tokens, do_sample=False)
        answers.append((row, tokenizer.decode(out[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True)))
    return progress.score(answers)
