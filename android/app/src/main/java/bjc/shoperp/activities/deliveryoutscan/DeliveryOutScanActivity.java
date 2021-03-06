package bjc.shoperp.activities.deliveryoutscan;

import android.app.AlertDialog;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.ActivityInfo;
import android.hardware.Camera;
import android.os.AsyncTask;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.support.v7.app.AppCompatActivity;
import android.text.TextUtils;
import android.util.Log;
import android.view.Menu;
import android.view.MenuInflater;
import android.view.MenuItem;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.view.View;
import android.view.WindowManager;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.TextView;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.BinaryBitmap;
import com.google.zxing.DecodeHintType;
import com.google.zxing.MultiFormatReader;
import com.google.zxing.PlanarYUVLuminanceSource;
import com.google.zxing.ReaderException;
import com.google.zxing.Result;
import com.google.zxing.common.HybridBinarizer;

import java.io.IOException;
import java.util.EnumMap;
import java.util.EnumSet;
import java.util.Map;

import bjc.shoperp.R;
import bjc.shoperp.activities.deliveryoutscan.camera.CameraManager;
import bjc.shoperp.domain.Order;
import bjc.shoperp.domain.OrderGoods;
import bjc.shoperp.domain.restfulresponse.domainresponse.OrderCollectionResponse;
import bjc.shoperp.service.restful.OrderService;
import bjc.shoperp.service.restful.ServiceContainer;

/*

 */
public final class DeliveryOutScanActivity extends AppCompatActivity implements SurfaceHolder.Callback, Camera.PreviewCallback {
    private static final String TAG = DeliveryOutScanActivity.class.getSimpleName();
    private ViewfinderView viewfinderView;
    private TextView statusView;
    private boolean hasSurface;
    private CameraManager cameraManager;
    private BeepManager beepManager;
    private AmbientLightManager ambientLightManager;

    private MultiFormatReader multiFormatReader;

    CameraManager getCameraManager() {
        return cameraManager;
    }

    @Override
    public void onCreate(Bundle icicle) {
        super.onCreate(icicle);
        setContentView(R.layout.activity_delivery_out_scan);
        //保持屏幕常亮
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        beepManager = new BeepManager(this);
        ambientLightManager = new AmbientLightManager(this);
        PreferenceManager.setDefaultValues(this, R.xml.delivery_out_scan_preferences, false);
        Map<DecodeHintType, Object> hints = new EnumMap<>(DecodeHintType.class);
        hints.put(DecodeHintType.POSSIBLE_FORMATS, EnumSet.allOf(BarcodeFormat.class));
        multiFormatReader = new MultiFormatReader();
        multiFormatReader.setHints(hints);
    }

    @Override
    protected void onResume() {
        super.onResume();

        // CameraManager must be initialized here, not in onCreate(). This is necessary because we don't
        // want to open the camera driver and measure the screen size if we're going to show the help on
        // first launch. That led to bugs where the scanning rectangle was the wrong size and partially
        // off screen.
        cameraManager = new CameraManager(getApplication());
        viewfinderView = (ViewfinderView) findViewById(R.id.viewfinder_view);
        statusView = (TextView) findViewById(R.id.status_view);

        //设置屏幕方向
        SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(this);
        String oriStr = prefs.getString(PreferencesActivity.KEY_AUTO_ORIENTATION, "垂直");
        int ori = ActivityInfo.SCREEN_ORIENTATION_PORTRAIT;
        if ("垂直".equalsIgnoreCase(oriStr)) {
            ori = ActivityInfo.SCREEN_ORIENTATION_PORTRAIT;
        } else if ("水平".equalsIgnoreCase(oriStr)) {
            ori = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
        } else if ("自动旋转".equalsIgnoreCase(oriStr)) {
            ori = ActivityInfo.SCREEN_ORIENTATION_SENSOR;
        } else if ("跟随系统".equalsIgnoreCase(oriStr)) {
            ori = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED;
        }
        setRequestedOrientation(ori);

        //启动声音振动
        beepManager.updatePrefs();
        //闪光灯自动控制
        ambientLightManager.start(cameraManager);

        //设置相机预览的控件，并初始化相机
        SurfaceView surfaceView = (SurfaceView) findViewById(R.id.preview_view);
        SurfaceHolder surfaceHolder = surfaceView.getHolder();
        if (hasSurface) {
            initCamera(surfaceHolder);
        } else {
            surfaceHolder.addCallback(this);
        }
    }

    @Override
    protected void onPause() {
        ambientLightManager.stop();
        beepManager.close();
        cameraManager.stopPreview();
        cameraManager.closeDriver();

        if (!hasSurface) {
            SurfaceView surfaceView = (SurfaceView) findViewById(R.id.preview_view);
            SurfaceHolder surfaceHolder = surfaceView.getHolder();
            surfaceHolder.removeCallback(this);
        }
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
    }

    @Override
    public boolean onCreateOptionsMenu(Menu menu) {
        MenuInflater menuInflater = getMenuInflater();
        menuInflater.inflate(R.menu.menu_deliveryoutscan, menu);
        return super.onCreateOptionsMenu(menu);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        Intent intent = new Intent(Intent.ACTION_VIEW);
        switch (item.getItemId()) {
            case R.id.menu_settings:
                intent.setClassName(this, PreferencesActivity.class.getName());
                startActivity(intent);
                break;
            default:
                return super.onOptionsItemSelected(item);
        }
        return true;
    }

    @Override
    public void surfaceCreated(SurfaceHolder holder) {
        if (!hasSurface) {
            hasSurface = true;
            initCamera(holder);
        }
    }

    @Override
    public void surfaceDestroyed(SurfaceHolder holder) {
        hasSurface = false;
    }

    @Override
    public void surfaceChanged(SurfaceHolder holder, int format, int width, int height) {
        if (!hasSurface) {
            hasSurface = true;
            initCamera(holder);
        }
    }

    public void handleMessage(String result, boolean detectBarcode, boolean isOk) {
        statusView.setText(result);
        if (detectBarcode)
            beepManager.playBeepSoundAndVibrate(isOk);
    }

    private void initCamera(SurfaceHolder surfaceHolder) {
        if (surfaceHolder == null) {
            throw new IllegalStateException("No SurfaceHolder provided");
        }
        if (cameraManager.isOpen()) {
            return;
        }
        try {
            cameraManager.openDriver(surfaceHolder);
            cameraManager.startPreview();
            this.startPreviewAndDecode();
        } catch (IOException ioe) {
            displayFrameworkBugMessageAndExit();
        } catch (RuntimeException e) {
            displayFrameworkBugMessageAndExit();
        }
    }

    private void startPreviewAndDecode() {
        Log.d("startPreviewAndDecode", "");
        viewfinderView.setVisibility(View.VISIBLE);
        viewfinderView.setFrameAndPreviewFramRect(cameraManager.getFramingRect(), cameraManager.getFramingRectInPreview());
        viewfinderView.drawViewfinder();
        cameraManager.requestPreviewFrame(this);
    }

    private void displayFrameworkBugMessageAndExit() {
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setTitle(getString(R.string.app_name));
        builder.setMessage(getString(R.string.msg_camera_framework_bug));
        builder.setPositiveButton(R.string.button_ok, new CameraErrorDailogListener(this));
        builder.setOnCancelListener(new CameraErrorDailogListener(this));
        builder.show();
    }

    @Override
    public void onPreviewFrame(byte[] data, Camera camera) {
        try {
            String s = PreferenceManager.getDefaultSharedPreferences(this).getString(PreferencesActivity.KEY_SCAN_WAIT, "1");
            String strWeight = ((EditText) findViewById(R.id.et_weight)).getText().toString().trim();
            Boolean chkPopState = ((CheckBox) findViewById(R.id.chk_pop_error)).isChecked();
            Boolean chkLocalState = ((CheckBox) findViewById(R.id.chk_local_state)).isChecked();
            int goodsCount = Integer.parseInt(strWeight);
            DecodeAndProcessTask task = new DecodeAndProcessTask(data, this, this.multiFormatReader, goodsCount, chkPopState, chkLocalState, Integer.parseInt(s));
            task.executeOnExecutor(AsyncTask.THREAD_POOL_EXECUTOR, (Void) null);
        } catch (Exception e) {
            Log.d("onPreviewFrame", e.getMessage());
        }
    }

    class DecodeAndProcessTask extends AsyncTask<Void, Void, Boolean> {
        private final byte[] rawBytes;
        private final DeliveryOutScanActivity activity;
        private final MultiFormatReader multiFormatReader;
        private final boolean checkPopError;
        private final boolean checkLocalState;
        private final int goodsCount;
        private String barcode = "";
        private String result = "";
        private int waitTime = 1;
        private boolean isSuccess = false;

        public DecodeAndProcessTask(byte[] rawBytes, DeliveryOutScanActivity activity, MultiFormatReader multiFormatReader, int goodsCount, boolean checkPopError, boolean checkLocalState, int waitTime) {
            this.rawBytes = rawBytes;
            this.activity = activity;
            this.multiFormatReader = multiFormatReader;
            this.checkPopError = checkPopError;
            this.checkLocalState = checkLocalState;
            this.goodsCount = goodsCount;
            this.waitTime = waitTime;
        }

        @Override
        protected Boolean doInBackground(Void... voids) {
            try {
                PlanarYUVLuminanceSource source = activity.getCameraManager().buildLuminanceSource(this.rawBytes);
                BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
                Result rawResult = multiFormatReader.decodeWithState(bitmap);
                this.barcode = rawResult.getText().trim();
                OrderCollectionResponse orderCollectionResponse = ServiceContainer.GetService(OrderService.class).markDelivery(rawResult.getText().trim(), this.goodsCount, this.checkPopError, this.checkLocalState);
                for (Order o : orderCollectionResponse.Datas) {
                    if (o.OrderGoodss != null) {
                        for (OrderGoods og : o.OrderGoodss) {
                            result += og.Vendor + " " + og.Number + " " + og.Edition + " " + og.Color + " " + og.Size + " " + og.Count + "\r";
                        }
                    }
                }
                result += orderCollectionResponse.Datas.get(0).ReceiverName + "," + orderCollectionResponse.Datas.get(0).ReceiverMobile + "," + orderCollectionResponse.Datas.get(0).ReceiverAddress;
                this.isSuccess = true;
            } catch (ReaderException re) {
                result = "未检测到条码,请对准条码";
            } catch (Exception e) {
                result = e.getMessage();
                if (TextUtils.isEmpty(result)) {
                    result = e.getClass().getName();
                }
            } finally {
                multiFormatReader.reset();
                this.publishProgress();
                try {
                    Thread.sleep(this.waitTime * 1000);
                } catch (InterruptedException e) {
                    e.printStackTrace();
                }
            }
            return true;
        }

        @Override
        protected void onProgressUpdate(Void... values) {
            activity.handleMessage(this.result, !TextUtils.isEmpty(this.barcode), this.isSuccess);
        }

        @Override
        protected void onPostExecute(Boolean aBoolean) {
            activity.startPreviewAndDecode();
        }
    }
}
