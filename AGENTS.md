# PROJECT KNOWLEDGE BASE

**Generated:** 2026-07-17

## OVERVIEW

Project: **MaaWuwa**

This repository is a MaaFramework project/template-derived automation project. The checked-in README and `assets/interface.json` still contain boilerplate names (`MaaPracticeBoilerplate` / `MaaXXX`), while the repository directory is `MaaWuwa` and assets include Wuthering Waves-specific images.

Stack:

- **MaaFramework** project resources in `assets/` using JSON/JSONC pipeline and interface files.
- **Python** custom agent/actions/recognition under `agent/`, using the `maafw` Python package (`maa.agent`, `maa.custom_action`, `maa.custom_recognition`).
- **Node.js** tooling for Maa resource checks via `@nekosu/maa-tools` and formatting via Prettier.
- **GitHub Actions** for checking resources, schema validation, packaging releases, and schema synchronization.

## STRUCTURE

- `assets/interface.json`: MaaFramework Project Interface V2 configuration for generic UI behavior, controllers, resources, tasks, and options.
- `assets/config/maa_pi_config.json`: MaaPi configuration; currently selects resource `官服` and fixed controller.
- `assets/resource/pipeline/`: Maa pipeline task definitions. `my_task.json` defines `MyTask1` through `MyTask4`; `MyTask4` demonstrates custom recognition/action hooks.
- `assets/resource/image/`: image/template assets (`empty.png`, `wuwa_connect.png`).
- `assets/resource/model/ocr/`: local OCR model files. This path is ignored by `assets/resource/model/.gitignore`; do not rely on committing large model files.
- `assets/MaaCommonAssets`: git submodule pointing to `MaaXYZ/MaaCommonAssets`.
- `agent/`: optional Python agent server and examples for custom action/recognition.
- `tools/`: helper scripts for OCR setup, packaging, and JSON schema validation.
- `deps/tools/`: MaaFramework JSON schemas used by VS Code and validation scripts.
- `docs/zh_cn/develop/`: Chinese development, FAQ, customization, and PR guidelines.
- `.github/workflows/`: CI checks (`check.yml`), packaging/release (`install.yml`), and schema sync.

## COMMANDS

| Action | Command |
| --- | --- |
| Install Node deps | `npm ci` |
| Install Python tool deps | `python -m pip install -r tools/requirements.txt` |
| Install MaaFW Python package for agent/checks | `python -m pip install --upgrade maafw --pre` |
| Configure OCR model from submodule assets | `python tools/configure.py` |
| Maa resource check | `npx @nekosu/maa-tools check` |
| Schema validation | `python tools/validate_schema.py --schema-dir deps/tools --resource-dirs assets/resource --exclude-dirs assets/resource/announcement --interface-files assets/interface.json` |
| Package locally | `python tools/install.py v0.0.1 linux x86_64` |
| Run custom agent directly | `python agent/main.py <socket_id>` |

Notes on commands:

- `tools/install.py` requires MaaFramework runtime files under `deps/bin` and `deps/share/MaaAgentBinary`; CI downloads them before packaging.
- Valid `tools/install.py` platform args are `win`, `macos`, `linux`, or `android` plus `x86_64` or `aarch64`.
- Normal end-user running/debugging is through MaaFramework tooling / a generic UI such as MFAAvalonia, not by starting a standalone app in this repo.
- To enable the Python agent in the UI/runtime, uncomment the `agent` block in `assets/interface.json`.

## CODING STANDARDS

- **JSON/JSONC resources**: 4-space indentation. `assets/**/*.json` is treated as JSONC in VS Code. Keep `assets/interface.json`, task entries, and pipeline node names consistent.
- **Prettier**: `.prettierrc` uses `tabWidth: 4`, `printWidth: 120`, `bracketSpacing: false`, `endOfLine: auto`, and `prettier-plugin-multiline-arrays`; YAML uses 2 spaces.
- **Python**: straightforward scripts with standard library plus explicit dependencies. VS Code recommends `ms-python.black-formatter`; keep functions simple and readable.
- **Markdown**: markdownlint rules live in `docs/.markdownlint.yaml`; line length is disabled, fenced code blocks should include a language.
- **Commits/PRs**: `docs/zh_cn/develop/pull_request_guidelines.md` recommends Conventional Commits such as `feat: ...`, `fix(ci): ...`, and focused PRs with verification records.

## WHERE TO LOOK

- **Primary Maa resources**: `assets/resource/` and `assets/interface.json`
- **Pipeline tasks**: `assets/resource/pipeline/my_task.json`
- **Custom Python hooks**: `agent/my_action.py`, `agent/my_reco.py`, `agent/main.py`
- **Packaging scripts**: `tools/install.py`, `tools/configure.py`
- **Validation logic**: `tools/validate_schema.py`, `deps/tools/*.schema.json`
- **Development docs**: `README.md`, `docs/zh_cn/develop/how_to_develop.md`, `docs/zh_cn/develop/faq.md`
- **CI workflows**: `.github/workflows/check.yml`, `.github/workflows/install.yml`

## NOTES

- Current project metadata is still partly boilerplate. If productizing the repo, update `README.md`, `assets/interface.json` (`name`, `description`, `github`, `contact`, task labels), issue templates, and artifact names in workflows.
- `assets/resource/model/ocr/` should be generated/downloaded locally; the template workflow can configure OCR resources during release packaging.
- `deps/tools/*.schema.json` are copied/synced from MaaFramework. VS Code schema associations expect these files to exist.
- CI `check.yml` runs `npm ci`, `npx @nekosu/maa-tools check`, installs `maafw --pre`, then runs `tools/validate_schema.py`.
- CI `install.yml` builds artifacts for `win`, `macos`, `linux`, and `android` on both `x86_64` and `aarch64`, then publishes zip files when a `v*` tag is pushed.
- There is currently an untracked asset at `assets/resource/image/wuwa_connect.png`; check `git status` before making commits.
