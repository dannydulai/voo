package com.ishiboo;
public interface EventListener<M> {
    void onNotify(Object source, M message);
}
