package javax.microedition.lcdui;
public abstract class Canvas extends Displayable {
    public static final int UP = 1, LEFT = 2, RIGHT = 5, DOWN = 6, FIRE = 8;
    public boolean isDoubleBuffered() { return true; }
    public int getGameAction(int k) {
        switch (k) {
            case -1: case 50: return UP;
            case -2: case 56: return DOWN;
            case -3: case 52: return LEFT;
            case -4: case 54: return RIGHT;
            case -5: case 53: return FIRE;
            default: return 0;
        }
    }
    public void repaint() {}
    public void serviceRepaints() {}
    protected abstract void paint(Graphics g);
    protected void keyPressed(int k) {}
    protected void keyReleased(int k) {}
    protected void keyRepeated(int k) {}
    protected void showNotify() {}
    protected void hideNotify() {}
}
