package com.ishiboo.voo;

import android.app.Application;

public class VooApplication extends Application {
    public VooConnection Connection = new VooConnection();

    @Override
    public void onTerminate() {
        this.Connection.Disconnect();
        super.onTerminate();
    }
}
