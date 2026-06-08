# Guardiao Models

The API and the edge worker now expect two local model artifacts:

- `haarcascade_frontalface_default.xml`
  - OpenCV face detector cascade file.
- `face-embedding.onnx`
  - ONNX face embedding model compatible with `112x112` RGB input and a single floating-point embedding output.

Expected default locations:

- local `dotnet run`: `models/haarcascade_frontalface_default.xml` and `models/face-embedding.onnx`
- docker compose: `/app/models/haarcascade_frontalface_default.xml` and `/app/models/face-embedding.onnx`

Notes:

- The repository does not vendor binary model artifacts.
- The detector file should come from the OpenCV model distribution.
- The embedding model should be ArcFace-style or another ONNX model that accepts a face crop and returns one embedding vector.
- If your model uses different input/output names, configure:
  - `BiometricProcessing__EmbeddingInputName`
  - `BiometricProcessing__EmbeddingOutputName`
  - `EdgeWorker__EmbeddingInputName`
  - `EdgeWorker__EmbeddingOutputName`
