# Bounded MR recognition experiments

This workflow adapts the experiment discipline of https://github.com/karpathy/autoresearch to this project's YOLO pipeline. It does not copy that project's training code or claim its language-model results apply to object detection.

## Current experiment

- Dataset: reviewed plant v20, with ambiguous TRAIN sample held out. Never change labels, split membership, image bytes, evaluation code or thresholds while comparing trials.
- Verify `Reports/plant-v20-autoresearch-contract.json` before and after each trial. A hash mismatch invalidates the comparison; preserve the failure evidence.
- Run only the two predeclared trials in `Reports/plant-v21-autoresearch-plan.json`, sequentially, 15 epochs each. This is an equal-epoch budget, not a fixed five-minute budget.
- The baseline uses lr0=0.0001. The second trial changes only lr0 to 0.0003. All other settings and the initializer remain fixed.
- Only full 13-image target VAL reports at confidence 0.35, image size 640, rectangular inference, NMS IoU 0.7 and max_det 8 can select checkpoints. The native 12-image auxiliary evaluation is diagnostic only.
- Rank with the frozen selector: precision >=0.9 and recall >=0.8 gate first, then F1, precision, recall and mAP50-95. Earliest checkpoint wins exact ties.

## Execution and evidence

Inspect the live process and output before starting anything. Never start a duplicate run or remove a lock owned by a live process. Keep rejected trials, native checkpoints, logs, metrics, environment versions, parameters and weight hashes. Do not delete inconvenient failures.

The runner is `train.py`; immutable preparation and verification are in `prepare.py`. The current runner executes predeclared bounded experiments; it is not an unbounded self-editing research agent. Any new experiment needs a new recorded hypothesis and bounded plan after inspecting these results.

## Acceptance boundaries

Repeated validation tuning does not demonstrate generalization. Never use TEST to choose parameters or checkpoints. Previously observed regression images are not a fresh independent test. A validation candidate still requires independent source/session-separated test evaluation and actual PICO images before deployment acceptance.

Never overwrite the lighter baseline, production scene profiles, or Unity assets as a side effect of research. A larger metric on a subset, training completion, or a simulated TCP/Unity check does not constitute PICO acceptance. Report measured inference latency and resource conditions separately from accuracy.

The full MR goal remains in force: models for the other scenes, one shared UI, scene-specific gestures and animation cleanup, stereo localization, Android build and device verification remain required.
