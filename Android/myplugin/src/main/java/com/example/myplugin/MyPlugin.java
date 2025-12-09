package com.example.myplugin;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Context;
import android.graphics.ImageFormat;
import android.hardware.camera2.CameraCaptureSession;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraDevice;
import android.hardware.camera2.CameraManager;
import android.media.Image;
import android.media.ImageReader;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;
import android.view.Surface;

import java.io.InputStream;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import ai.onnxruntime.OnnxTensor;
import ai.onnxruntime.OrtEnvironment;
import ai.onnxruntime.OrtSession;

public class MyPlugin {

    private static String TAG = "8.10 ";

    // =========================================================
    // General state
    // =========================================================
    private static Activity unityActivity;
    private static int targetClassId = -1;

    private static float latestBrightness = 0f;

    private static HandlerThread cameraThread;
    private static Handler cameraHandler;

    private static boolean running = false;

    private static final String KEY_CAMERA_POSITION = "com.meta.extra_metadata.position";
    private static final String KEY_CAMERA_SOURCE   = "com.meta.extra_metadata.camera_source";

    private static final int CAMERA_SOURCE_PASSTHROUGH = 0;
    private static final int POSITION_LEFT  = 0;
    private static final int POSITION_RIGHT = 1;

    // Camera identifiers
    private static String leftCameraId  = null;
    private static String rightCameraId = null;

    private static CameraDevice leftCamera  = null;
    private static CameraDevice rightCamera = null;

    private static CameraCaptureSession leftSession  = null;
    private static CameraCaptureSession rightSession = null;

    private static ImageReader leftReader  = null;
    private static ImageReader rightReader = null;

    private static boolean loggedOutputShape = false;

    // =========================================================
    // ONNX Runtime
    // =========================================================
    private static OrtEnvironment ortEnv;

    // ⭐ Preloaded model sessions ⭐
    private static OrtSession forkSession  = null;
    private static OrtSession plateSession = null;

    // Active inference session
    private static OrtSession ortSession = null;

    private static boolean modelReady = false;

    private static final String FORK_MODEL  = "fork_yolo11n_sam4_r320_aug.onnx";
    private static final String PLATE_MODEL = "plate_yolo11n_sam3344_r320_aug_v2.onnx";

    // Used for FPS measurement
    private static long lastInferenceTime = System.currentTimeMillis();
    private static int inferenceCount = 0;
    private static float inferencesPerSecond = 0f;

    private static final float DETECTION_THRESHOLD = 0.35f;

    // Lock for inference + switching
    private static final Object inferenceLock = new Object();

    // =========================================================
    // Bounding Box storage
    // =========================================================
    public static class BoundingBox {
        public float xmin, ymin, xmax, ymax, score;
        public int classId;
    }

    private static final int MAX_DETECTIONS = 20;

    private static final BoundingBox[] detectedBoxesLeft  = new BoundingBox[MAX_DETECTIONS];
    private static final BoundingBox[] detectedBoxesRight = new BoundingBox[MAX_DETECTIONS];

    private static int detectedCountLeft  = 0;
    private static int detectedCountRight = 0;

    private static boolean objectDetectedLeft  = false;
    private static boolean objectDetectedRight = false;

    static {
        for (int i = 0; i < MAX_DETECTIONS; i++) {
            detectedBoxesLeft[i]  = new BoundingBox();
            detectedBoxesRight[i] = new BoundingBox();
        }
    }

    // =========================================================
    // Init (Unity calls this once)
    // =========================================================
    public static String init(Object activity, int classId) {
        unityActivity = (Activity) activity;
        targetClassId = classId;

        try {
            ortEnv = OrtEnvironment.getEnvironment();

            // ⭐ PRELOAD BOTH MODELS ⭐
            forkSession  = loadModelFromAssets(FORK_MODEL);
            plateSession = loadModelFromAssets(PLATE_MODEL);

            // Default model
            ortSession = plateSession;
            modelReady = true;

            Log.i(TAG, "Both models preloaded successfully.");

        } catch (Exception e) {
            Log.e(TAG, "ONNX preload failed!", e);
            modelReady = false;
        }

        return TAG;
    }

    // =========================================================
    // Load a model into a NEW OrtSession (only called at startup)
    // =========================================================
    private static OrtSession loadModelFromAssets(String modelName) {
        try {
            InputStream is = unityActivity.getAssets().open(modelName);
            byte[] bytes = new byte[is.available()];
            is.read(bytes);
            is.close();

            OrtSession.SessionOptions opts = new OrtSession.SessionOptions();
            return ortEnv.createSession(bytes, opts);

        } catch (Exception e) {
            Log.e(TAG, "Failed loading model: " + modelName, e);
            return null;
        }
    }

    // =========================================================
    // Ultra-fast model switching (just swaps pointer)
    // =========================================================
    public static void switchModel(int modelType) {

        synchronized (inferenceLock) {

            if (modelType == 0) {
                ortSession = forkSession;
                Log.i(TAG, "Switched to FORK model");
            }
            else {
                ortSession = plateSession;
                Log.i(TAG, "Switched to PLATE model");
            }

            modelReady = true;
        }
    }

    // =========================================================
    // Camera control
    // =========================================================
    public static void startCamera() {
        if (running) return;

        running = true;

        cameraThread = new HandlerThread("QuestCameraThread");
        cameraThread.start();
        cameraHandler = new Handler(cameraThread.getLooper());

        unityActivity.runOnUiThread(() ->
                openPassthroughCameras(unityActivity)
        );

        switchModel(1);
    }

    public static void stopCamera() {
        running = false;

        try { if (leftSession != null) leftSession.close(); } catch (Exception ignored) {}
        try { if (rightSession != null) rightSession.close(); } catch (Exception ignored) {}
        try { if (leftCamera != null) leftCamera.close(); } catch (Exception ignored) {}
        try { if (rightCamera != null) rightCamera.close(); } catch (Exception ignored) {}
        try { if (leftReader != null) leftReader.close(); } catch (Exception ignored) {}
        try { if (rightReader != null) rightReader.close(); } catch (Exception ignored) {}
        try { if (cameraThread != null) cameraThread.quitSafely(); } catch (Exception ignored) {}

        leftReader = rightReader = null;
        leftCamera = rightCamera = null;
        leftSession = rightSession = null;
    }

    // =========================================================
    // Camera discovery
    // =========================================================
    @SuppressLint("MissingPermission")
    private static void openPassthroughCameras(Context ctx) {
        try {
            CameraManager cm = (CameraManager) ctx.getSystemService(Context.CAMERA_SERVICE);

            leftCameraId  = null;
            rightCameraId = null;

            for (String id : cm.getCameraIdList()) {
                CameraCharacteristics cc = cm.getCameraCharacteristics(id);

                Integer src = cc.get(new CameraCharacteristics.Key<>(KEY_CAMERA_SOURCE, Integer.class));
                Integer pos = cc.get(new CameraCharacteristics.Key<>(KEY_CAMERA_POSITION, Integer.class));

                if (src != null && src == CAMERA_SOURCE_PASSTHROUGH) {
                    if (pos != null && pos == POSITION_LEFT)  leftCameraId = id;
                    if (pos != null && pos == POSITION_RIGHT) rightCameraId = id;
                }
            }

            if (leftCameraId != null)
                openSingleCamera(cm, leftCameraId, true);

            if (rightCameraId != null)
                openSingleCamera(cm, rightCameraId, false);

        } catch (Exception e) {
            Log.e(TAG, "Error opening passthrough cameras", e);
        }
    }

    @SuppressLint("MissingPermission")
    private static void openSingleCamera(CameraManager cm, String cameraId, boolean isLeft) {

        try {
            ImageReader reader = ImageReader.newInstance(
                    320, 240, ImageFormat.YUV_420_888, 2);

            if (isLeft) leftReader = reader;
            else        rightReader = reader;

            reader.setOnImageAvailableListener(MyPlugin::handleImage, cameraHandler);

            cm.openCamera(cameraId, new CameraDevice.StateCallback() {

                @Override
                public void onOpened(CameraDevice camera) {
                    if (isLeft) leftCamera  = camera;
                    else        rightCamera = camera;

                    startCameraSession(camera, reader.getSurface(), isLeft);
                }

                @Override public void onDisconnected(CameraDevice camera) {}
                @Override public void onError(CameraDevice camera, int error) {}

            }, cameraHandler);

        } catch (Exception e) {
            Log.e(TAG, "openSingleCamera failed", e);
        }
    }

    private static void startCameraSession(CameraDevice camera, Surface surface, boolean isLeft) {

        try {
            List<Surface> outputs = new ArrayList<>();
            outputs.add(surface);

            camera.createCaptureSession(outputs, new CameraCaptureSession.StateCallback() {

                @Override
                public void onConfigured(CameraCaptureSession session) {

                    if (isLeft) leftSession = session;
                    else        rightSession = session;

                    startRepeating(session, surface);
                }

                @Override
                public void onConfigureFailed(CameraCaptureSession session) {
                    Log.e(TAG, "Session config failed");
                }

            }, cameraHandler);

        } catch (Exception e) {
            Log.e(TAG, "startCameraSession error", e);
        }
    }

    private static void startRepeating(CameraCaptureSession session, Surface surface) {
        try {
            CameraDevice camera = session.getDevice();
            if (camera == null) return;

            CameraCaptureSession captureSession = session;

            android.hardware.camera2.CaptureRequest.Builder req =
                    camera.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);

            req.addTarget(surface);

            captureSession.setRepeatingRequest(req.build(), null, cameraHandler);

        } catch (Exception e) {
            Log.e(TAG, "Repeating error", e);
        }
    }

    // =========================================================
    // Frame callback → detection
    // =========================================================
    private static void handleImage(ImageReader reader) {

        Image img = reader.acquireLatestImage();
        if (img == null) return;

        boolean isLeft = (reader == leftReader);

        if (isLeft)
            latestBrightness = computeAverageLuma(img);

        if (modelReady)
            runObjectDetection(img, isLeft);

        img.close();
    }

    private static float computeAverageLuma(Image img) {
        Image.Plane yPlane = img.getPlanes()[0];
        ByteBuffer y = yPlane.getBuffer();

        long sum = 0;
        int count = y.remaining();
        if (count == 0) return 0f;

        while (y.hasRemaining()) sum += (y.get() & 0xFF);

        return (sum / (float) count) / 255f;
    }

    // =========================================================
    // YUV → RGB
    // =========================================================
    private static void YUV420toRGB(Image image, int[] outRGB) {

        Image.Plane yPlane = image.getPlanes()[0];
        Image.Plane uPlane = image.getPlanes()[1];
        Image.Plane vPlane = image.getPlanes()[2];

        ByteBuffer yBuf = yPlane.getBuffer();
        ByteBuffer uBuf = uPlane.getBuffer();
        ByteBuffer vBuf = vPlane.getBuffer();

        int yRow = yPlane.getRowStride();
        int yPix = yPlane.getPixelStride();

        int uRow = uPlane.getRowStride();
        int uPix = uPlane.getPixelStride();

        int vRow = vPlane.getRowStride();
        int vPix = vPlane.getPixelStride();

        int W = image.getWidth();
        int H = image.getHeight();

        int index = 0;

        for (int y = 0; y < H; y++) {

            int pY = yRow * y;
            int uvRow = y >> 1;
            int pU = uRow * uvRow;
            int pV = vRow * uvRow;

            for (int x = 0; x < W; x++, index++) {

                int Y = (yBuf.get(pY + x * yPix) & 0xFF);
                int U = (uBuf.get(pU + (x >> 1) * uPix) & 0xFF) - 128;
                int V = (vBuf.get(pV + (x >> 1) * vPix) & 0xFF) - 128;

                int R = Y + (int)(1.402f * V);
                int G = Y - (int)(0.344f * U) - (int)(0.714f * V);
                int B = Y + (int)(1.772f * U);

                R = Math.max(0, Math.min(255, R));
                G = Math.max(0, Math.min(255, G));
                B = Math.max(0, Math.min(255, B));

                outRGB[index] = (R << 16) | (G << 8) | B;
            }
        }
    }

    // =========================================================
    // Resize
    // =========================================================
    private static float[][][] letterboxResize(int[] rgb, int srcW, int srcH) {

        float[][][] out = new float[3][320][320];

        float scale = Math.min(320f / srcW, 320f / srcH);
        int newW = Math.round(srcW * scale);
        int newH = Math.round(srcH * scale);

        int padX = (320 - newW) / 2;
        int padY = (320 - newH) / 2;

        for (int y = 0; y < newH; y++) {

            float ys = y / scale;
            int y0 = (int) ys;
            int y1 = Math.min(y0 + 1, srcH - 1);
            float yL = ys - y0;

            for (int x = 0; x < newW; x++) {

                float xs = x / scale;
                int x0 = (int) xs;
                int x1 = Math.min(x0 + 1, srcW - 1);
                float xL = xs - x0;

                int idx00 = y0 * srcW + x0;
                int idx01 = y0 * srcW + x1;
                int idx10 = y1 * srcW + x0;
                int idx11 = y1 * srcW + x1;

                int c00 = rgb[idx00], c01 = rgb[idx01];
                int c10 = rgb[idx10], c11 = rgb[idx11];

                float r = lerp2(
                        (c00 >> 16) & 0xFF, (c01 >> 16) & 0xFF,
                        (c10 >> 16) & 0xFF, (c11 >> 16) & 0xFF,
                        xL, yL);

                float g = lerp2(
                        (c00 >> 8) & 0xFF, (c01 >> 8) & 0xFF,
                        (c10 >> 8) & 0xFF, (c11 >> 8) & 0xFF,
                        xL, yL);

                float b = lerp2(
                        c00 & 0xFF, c01 & 0xFF,
                        c10 & 0xFF, c11 & 0xFF,
                        xL, yL);

                int xx = x + padX;
                int yy = y + padY;

                out[0][yy][xx] = r / 255f;
                out[1][yy][xx] = g / 255f;
                out[2][yy][xx] = b / 255f;
            }
        }

        return out;
    }

    private static float lerp2(float c00, float c01, float c10, float c11, float tx, float ty) {
        float a = c00 * (1 - tx) + c01 * tx;
        float b = c10 * (1 - tx) + c11 * tx;
        return a * (1 - ty) + b * ty;
    }

    // =========================================================
    // YOLO inference
    // =========================================================
    private static boolean runObjectDetection(Image img, boolean isLeftCamera) {

        synchronized (inferenceLock) {

            if (ortSession == null)
                return false;

            boolean detectedLocal = false;
            int countLocal = 0;

            BoundingBox[] targetBoxes =
                    isLeftCamera ? detectedBoxesLeft : detectedBoxesRight;

            try {
                int W = img.getWidth();
                int H = img.getHeight();

                int[] rgb = new int[W * H];
                YUV420toRGB(img, rgb);

                float[][][] nchw = letterboxResize(rgb, W, H);

                float[][][][] input = new float[1][][][];
                input[0] = nchw;

                // Inference
                OnnxTensor tensor = OnnxTensor.createTensor(ortEnv, input);
                OrtSession.Result result = ortSession.run(
                        Collections.singletonMap(
                                ortSession.getInputNames().iterator().next(),
                                tensor));

                float[][][] output = (float[][][]) result.get(0).getValue();
                int numProps = output[0][0].length;

                ArrayList<BoundingBox> cand = new ArrayList<>();

                for (int i = 0; i < numProps; i++) {

                    float cx = output[0][0][i];
                    float cy = output[0][1][i];
                    float ww = output[0][2][i];
                    float hh = output[0][3][i];
                    float score = output[0][4][i];

                    if (score < DETECTION_THRESHOLD)
                        continue;

                    float xmin = Math.max(0, Math.min(1, (cx - ww / 2f) / 320f));
                    float ymin = Math.max(0, Math.min(1, (cy - hh / 2f) / 320f));
                    float xmax = Math.max(0, Math.min(1, (cx + ww / 2f) / 320f));
                    float ymax = Math.max(0, Math.min(1, (cy + hh / 2f) / 320f));

                    BoundingBox b = new BoundingBox();
                    b.xmin = xmin;
                    b.ymin = ymin;
                    b.xmax = xmax;
                    b.ymax = ymax;
                    b.score = score;
                    b.classId = 0;

                    cand.add(b);
                }

                cand.sort((a, b) -> Float.compare(b.score, a.score));
                boolean[] removed = new boolean[cand.size()];

                for (int i = 0; i < cand.size(); i++) {
                    if (removed[i]) continue;

                    BoundingBox a = cand.get(i);

                    if (countLocal < MAX_DETECTIONS) {

                        BoundingBox out = targetBoxes[countLocal++];
                        out.xmin = a.xmin;
                        out.ymin = a.ymin;
                        out.xmax = a.xmax;
                        out.ymax = a.ymax;
                        out.score = a.score;
                        out.classId = a.classId;

                        detectedLocal = true;
                    }

                    for (int j = i + 1; j < cand.size(); j++) {
                        if (!removed[j] && iou(a, cand.get(j)) > 0.45f)
                            removed[j] = true;
                    }
                }

            } catch (Exception e) {
                Log.e(TAG, "Detection error", e);
            }

            if (isLeftCamera) {
                objectDetectedLeft = detectedLocal;
                detectedCountLeft = countLocal;
            } else {
                objectDetectedRight = detectedLocal;
                detectedCountRight = countLocal;
            }

            return detectedLocal;
        }
    }

    private static float iou(BoundingBox a, BoundingBox b) {

        float ixmin = Math.max(a.xmin, b.xmin);
        float iymin = Math.max(a.ymin, b.ymin);
        float ixmax = Math.min(a.xmax, b.xmax);
        float iymax = Math.min(a.ymax, b.ymax);

        float iw = Math.max(0, ixmax - ixmin);
        float ih = Math.max(0, iymax - iymin);

        float inter = iw * ih;
        float union =
                (a.xmax - a.xmin) * (a.ymax - a.ymin) +
                        (b.xmax - b.xmin) * (b.ymax - b.ymin) -
                        inter;

        return union <= 0 ? 0 : inter / union;
    }

    // =========================================================
    // Unity Getters
    // =========================================================
    public static boolean isObjectDetectedLeft()  { return objectDetectedLeft; }
    public static boolean isObjectDetectedRight() { return objectDetectedRight; }

    public static boolean hasStereoDetections() {
        return (detectedCountLeft > 0 && detectedCountRight > 0);
    }

    public static float[] getStereoBoundingBoxes() {

        // If either side has no detections, return an empty array.
        if (detectedCountLeft == 0 || detectedCountRight == 0)
            return new float[0];

        BoundingBox L = detectedBoxesLeft[0];
        BoundingBox R = detectedBoxesRight[0];

        return new float[] {
                L.xmin, L.ymin, L.xmax, L.ymax, L.score, L.classId,
                R.xmin, R.ymin, R.xmax, R.ymax, R.score, R.classId
        };
    }

}
