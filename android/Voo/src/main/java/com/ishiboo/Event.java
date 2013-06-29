package com.ishiboo;
import java.util.Arrays;

public class Event<M> {
    public static class ListenerRegistration {
        private final Event<?> event;
        private final EventListener<?> listener;
        private boolean unregistered;

        private <M> ListenerRegistration(final Event<M> event, final EventListener<M> listener) {
            this.event = event;
            this.listener = listener;
        }

        public void remove() {
            if (!unregistered) {
                unregistered = true;
                event.remove(listener);
            }
        }
        public boolean isSubscribed() {
            return !unregistered;
        }
    }

    private Object[] listeners;

    public ListenerRegistration add(final EventListener<M> listener) {
        synchronized (this) {
            if (listeners == null) {
                listeners = new Object[] { listener };
            } else {
                listeners = Arrays.copyOf(listeners, listeners.length + 1);
                listeners[listeners.length - 1] = listener;
            }
        }
        return new ListenerRegistration(this, listener);
    }

    public void dispatch(final Object source, final M message) {
        final Object[] local = listeners;
        if (local != null) {
            for (final Object listener : local) {
                ((EventListener<M>) listener).onNotify(source, message);
            }
        }
    }
    
    private void remove(final EventListener<?> listener) {
        synchronized (this) {
            if (listeners.length > 1) {
                final Object[] newObservers = new Object[listeners.length - 1];
                for (int i = 0, j = 0; i < listeners.length; i++) {
                    if (!listeners[i].equals(listener)) {
                        newObservers[j++] = listeners[i];
                    }
                }
                listeners = newObservers;
            } else {
                listeners = null;
            }
        }
    }
}
