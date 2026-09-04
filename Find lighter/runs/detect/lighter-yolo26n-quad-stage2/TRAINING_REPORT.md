# YOLO26n QUAD v3 Training Report

## Dataset

- Existing lighter dataset: 244 images
- QUAD lighter dataset: 309 images (CC BY 4.0)
- Combined total: 553 images
- Train: 407 images / 436 boxes
- Validation: 100 images / 103 boxes
- Test: 46 images / 48 boxes
- Exact duplicate images between sources: 0
- QUAD polygon labels were converted to enclosing detection boxes.

## Training

- Base checkpoint: `best_yolo26n_v2.pt`
- Stage 1: 30 epochs, layers 0-9 frozen
- Stage 2: 60 epochs, all layers trainable
- Image size: 640
- Batch size: 16
- Stage 2 optimizer: AdamW
- Stage 2 learning rate: 0.001 with cosine decay
- Device: NVIDIA GeForce RTX 5070 Laptop GPU

## Final validation metrics

- Precision: 0.932
- Recall: 0.928
- mAP50: 0.968
- mAP50-95: 0.817

## Independent test metrics

- Precision: 0.977
- Recall: 0.893
- mAP50: 0.968
- mAP50-95: 0.822
- Test images: 46
- Test instances: 48

## Deployment

- Runtime checkpoint: `object-detection-demo/weights/best_yolo26n_quad_v3.pt`
- Default inference confidence: 0.45
- Default maximum detections per frame: 1

## Limitation

The combined dataset contains essentially no negative/background-only images. The reported precision measures annotated positive-scene performance and does not fully characterize false positives in empty real-world PICO scenes. Add hard-negative PICO frames in the next dataset revision.
