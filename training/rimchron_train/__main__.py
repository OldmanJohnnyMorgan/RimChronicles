"""python -m rimchron_train <step> [--config config/d0.yaml]"""
from __future__ import annotations

import argparse
import sys


def main() -> int:
    p = argparse.ArgumentParser(prog="rimchron_train", description=__doc__)
    p.add_argument("step", choices=["env", "data", "sft", "export"])
    p.add_argument("--config", default="config/d0.yaml")
    p.add_argument("--base-modelfile", help="export: a saved `ollama show <base> --modelfile`, if ollama isn't callable from WSL")
    p.add_argument("--modelfiles-only", action="store_true",
                   help="export: only write the Modelfiles for GGUFs already exported (skips loading and converting)")
    a = p.parse_args()

    from . import config

    cfg = config.load(a.config)
    if a.step == "env":
        from . import env
        return env.run(cfg.base_model)
    if a.step == "data":
        from . import data
        m = data.check_manifest(cfg.dataset)
        print(f"{cfg.dataset}: prompt {m['promptVersion']}, tier {m['tier']}, rows {m['rows']}, cases {m['cases']}")
        return 0
    if a.step == "sft":
        from . import sft
        return sft.run(cfg)
    from . import export
    return export.run(cfg, a.base_modelfile, a.modelfiles_only)


if __name__ == "__main__":
    sys.exit(main())
