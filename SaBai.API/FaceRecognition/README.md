# Face recognition

ONNX Runtime pipeline with embeddings stored in Qdrant.

1. **Detection**: [SCRFD](https://arxiv.org/abs/2105.04714) (`det_10g.onnx`) finds face boxes and five landmarks.
2. **Alignment**: the landmarks are mapped onto the standard ArcFace 112×112 template with a least-squares similarity transform.
3. **Recognition**: [ArcFace](https://arxiv.org/abs/1801.07698) ResNet-50 trained on WebFace600K (`w600k_r50.onnx`) produces a 512-d, L2-normalised embedding.
4. **Search**: embeddings live in the Qdrant collection `faces` (cosine distance), with a `person_id` payload index.

Both models come from InsightFace's `buffalo_l` pack. The first time they're needed, they're downloaded (~280 MB) to
`FaceRecognition:ModelsDirectory`, which defaults to `~/.local/share/sabai/models`. In containers, mount a volume
there or pre-provision the files and set `FaceRecognition:AutoDownloadModels` to `false`. Any SCRFD (3 strides, with
landmarks) or ArcFace ONNX export works, for example `antelopev2`'s `glintr100.onnx`; the Qdrant collection is sized
from the model's output.

The pipeline uses InsightFace's RGB normalization and ArcFace alignment template. Detector resizing
uses bilinear interpolation when shrinking as well as enlarging, rather than smoothing small faces
with area averaging. Alignment also uses bilinear interpolation;
pixel-identical parity with the reference Python/OpenCV implementation is not guaranteed.

Endpoints require an authenticated staff user (`Admin` or `Member` role).

> **License:** InsightFace's pretrained models are for **non-commercial research use only**. Commercial use requires a
> license from InsightFace or a different model.

## Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/faces/detect` | Boxes, scores and landmarks for every face |
| `POST` | `/persons/{personId}/faces` | Enroll the most prominent face for a person |
| `GET` | `/persons/{personId}/faces` | List a person's enrolled faces |
| `DELETE` | `/persons/{personId}` | Remove all of a person's faces |
| `DELETE` | `/faces/{faceId}` | Remove one enrolled face |
| `POST` | `/faces/identify` | Match every face in an image against enrolled people (`threshold`, `maxMatches` query) |
| `POST` | `/faces/verify` | 1:1 similarity between the main faces in two images |

Images are sent as `multipart/form-data` (`image`, or `first`/`second` for verify). Similarity is cosine in
`[-1, 1]`; the default `MatchThreshold` is `0.4`. It is a starting point, not a calibrated probability
or a guarantee of identity. Enrolling several sharp, well-lit photos per person improves recall.

## Accuracy settings

`MinimumFaceSize` and `MinimumEyeDistance` now default to **zero (disabled)**. The previous 40-pixel
face / 12-pixel eye-distance cutoffs could discard small faces and oblique views before matching.
Only invalid geometry (non-finite coordinates, empty boxes or non-invertible alignment) is rejected
by default. Optional size guards still apply to recognition when explicitly configured.

Detection and recognition use the same best-effort detection strategy:

1. Run the original 640-pixel / `DetectionThreshold = 0.5` pass.
2. If no face is found, or a face is under 64 pixels in either source dimension, retry at
   `DetectionFallbackSize = 1024` and `DetectionFallbackThreshold = 0.35`. Merge new faces without
   replacing overlapping primary landmarks. Fixed-size ONNX exports use their declared size.
3. If still no face is found, retry with bounded shadow lifting.
4. If still unsuccessful, retry at -25 and +25 degrees of image roll on expanded canvases, mapping
   landmarks and boxes back to the original image. This helps tilt, not extreme profile/yaw.

Set `EnableDetectionFallback = false` for a single pass. Retries cost additional CPU/memory and can
increase false detections. Lower **detector** confidence does not lower `MatchThreshold`. Recognition
always uses the original image pixels; no synthetic face detail, brightness-adjusted embeddings,
or changed ArcFace template is introduced. Existing stored embeddings are retained. Landmark
changes can still affect similarity, so compare against representative enrolled photos.

## Children, adolescents and growth

These are recall-oriented preprocessing changes, **not a pediatric-trained or age-invariant model**.
The existing `w600k_r50` weights have not been retrained, and no child-specific accuracy claim is made.
Do not alter the ArcFace template to approximate child proportions: it must match model training.
Very low resolution, severe blur and hidden features cannot be recovered by preprocessing.

Use multiple **staff-confirmed** enrollment photos for the same person/patient ID: a frontal view,
modest left/right angles, and photos from different visits/ages. Enrollment adds a reference rather
than replacing older ones; search takes the strongest similarity across that person's references.
Keep earlier references and add a recent photo as the child grows. This provides age-spanning
references but cannot guarantee matching a single age-5 enrollment to an age-10 query. Verification
of two individual photos (`/faces/verify`) has no access to that enrollment history.

Candidate retrieval expands when multiple references from one person crowd out other identities,
up to 4096 vectors per query. This is bounded best-effort retrieval, not guaranteed exhaustive
per-person search for arbitrarily large reference histories. Scores are not averaged across ages
or given an age-based bonus.

Never auto-enroll an uncertain search result or assume growth justifies lowering the identity
threshold. Confirm identity with other patient information, especially for siblings/lookalikes.
If reliable five-year age gaps are required, evaluate a licensed age-robust model on consented
longitudinal pediatric data. A model replacement needs a separate collection and re-embedding all
references, even if embedding dimensions happen to match.

Before production tuning, evaluate labeled same-person and different-person pairs from the actual
camera workflow, keeping identities separate between calibration and evaluation sets. Choose
`MatchThreshold` using measured false-match and false-non-match rates, and check lighting, pose and
age bands (including ages 5 and 10), time gaps, siblings/lookalikes and demographic slices. Do not
lower the threshold just to force a match. Model weights and the default
match threshold have not been changed, and accuracy gains require validation on those images.
