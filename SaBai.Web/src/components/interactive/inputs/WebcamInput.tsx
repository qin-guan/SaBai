import { useToggle } from "@/lib/hooks/useToggle";
import Image from "next/image";
import { useCallback, useEffect, useRef, useState } from "react";
import Webcam from "react-webcam";
import toast from "react-hot-toast";
import { Button } from "@/components/interactive/Button/Button";

const videoConstraints = {
  width: 720,
  height: 720,
  facingMode: "environment",
};

export function WebcamInput({
  imageDetails,
  setImageDetails,
  cameraIsOpenCallback,
  size = 250,
  autoOpenCamera = true,
}: {
  imageDetails: string | null;
  setImageDetails: (picture: string | null) => void;
  cameraIsOpenCallback?: (isOpen: boolean) => void;
  size?: number;
  /** Open the camera immediately when no image is captured. */
  autoOpenCamera?: boolean;
}) {
  const [cameraIsOpen, , setCameraIsOpen] = useToggle(autoOpenCamera);

  // Until the browser hands us a live media stream, `getScreenshot()` returns
  // null (see react-webcam's `getScreenshot`/`getCanvas`). Without tracking that,
  // clicking Capture silently does nothing. Track readiness and the last error
  // from the camera so the failure is visible instead.
  const [cameraReady, setCameraReady] = useState(false);
  const [cameraError, setCameraError] = useState<string | null>(null);

  const openCamera = useCallback(() => {
    // Fresh stream, so wait for `onUserMedia` again before allowing capture.
    setCameraReady(false);
    setCameraError(null);
    setCameraIsOpen(true);
  }, [setCameraIsOpen]);

  const webcamRef = useRef<Webcam>(null);
  const webcamCapture = useCallback(() => {
    const imgSrc = webcamRef.current?.getScreenshot() ?? null;
    if (!imgSrc) {
      toast.error(
        cameraError ??
          "Camera isn't ready yet. Allow camera access and try again.",
      );
      return;
    }
    setImageDetails(imgSrc);
  }, [setImageDetails, cameraError]);

  useEffect(() => {
    // close camera if image is already available (either photo just taken, or
    // photo is already available when you open registration modal)
    if (imageDetails) setCameraIsOpen(false);
    // open camera automatically if there is no image (unless opted out)
    else if (autoOpenCamera) setCameraIsOpen(true);
  }, [imageDetails, setCameraIsOpen, autoOpenCamera]);

  useEffect(() => {
    if (cameraIsOpenCallback) cameraIsOpenCallback(cameraIsOpen);
  }, [cameraIsOpen, cameraIsOpenCallback]);

  return (
    <div className="flex flex-col items-center">
      <label className="block text-sm font-medium">
        Photo<span className="text-red-500">*</span>
      </label>
      {!cameraIsOpen && (
        <div
          className="relative flex items-center justify-center bg-gray-400 w-full aspect-square"
          style={{ maxWidth: size }}
        >
          {imageDetails != null && (
            <Image src={imageDetails} alt="" fill={true} unoptimized={true} />
          )}
        </div>
      )}

      {cameraIsOpen && (
        <div
          className="flex flex-col items-center justify-center space-y-2 w-full"
          style={{ maxWidth: size }}
        >
          <Webcam
            audio={false}
            width="100%"
            ref={webcamRef}
            screenshotFormat="image/jpeg"
            screenshotQuality={1}
            videoConstraints={videoConstraints}
            id="webcam"
            onUserMedia={() => {
              setCameraReady(true);
              setCameraError(null);
            }}
            onUserMediaError={(error) => {
              setCameraReady(false);
              setCameraError(
                typeof error === "string" ? error : error?.message,
              );
            }}
          />
          <Button
            title="Capture"
            colour="emerald"
            disabled={!cameraReady}
            onClick={webcamCapture}
          />
          {cameraError && (
            <p className="text-center text-sm text-red-500">
              Camera unavailable: {cameraError}
            </p>
          )}
        </div>
      )}
      <div className="mt-6 flex items-center justify-center">
        {!cameraIsOpen ? (
          imageDetails == null ? (
            <Button colour="emerald" title="Take Photo" onClick={openCamera} />
          ) : (
            <Button
              colour="red"
              title="Retake Photo"
              onClick={() => {
                openCamera();
                setImageDetails(null);
              }}
            />
          )
        ) : (
          // don't show cancel button when there is no image captured yet
          imageDetails != null && (
            <Button
              colour="red"
              title="Cancel"
              onClick={() => setCameraIsOpen(false)}
            />
          )
        )}
      </div>
    </div>
  );
}
