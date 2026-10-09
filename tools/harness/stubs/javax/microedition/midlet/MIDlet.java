package javax.microedition.midlet;
public abstract class MIDlet {
    protected MIDlet() {}
    protected abstract void startApp();
    protected abstract void pauseApp();
    protected abstract void destroyApp(boolean u);
    public final void notifyDestroyed() {}
}
