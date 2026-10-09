package javax.microedition.lcdui;
public abstract class Displayable {
    CommandListener listener;
    public void addCommand(Command c) {}
    public void removeCommand(Command c) {}
    public void setCommandListener(CommandListener l) { listener = l; }
    public void setTitle(String s) {}
    public int getWidth() { return 176; }
    public int getHeight() { return 144; }
}
