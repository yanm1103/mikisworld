import java.io.*;
import java.lang.reflect.*;
import java.util.*;
import javax.microedition.lcdui.*;

/**
 * Golden-trace generator: runs the ORIGINAL MikisWorld.jar bytecode headless (against stub MIDP classes),
 * feeds it a scripted input sequence with a fixed 20 ms tick and a seeded java.util.Random, and prints
 * the game state after every tick. The C# port must reproduce these lines exactly.
 *
 * usage: java Trace <mode: main|bonus> <seed> <ticks> <inputScript|-> [startArea]
 * input script lines:  <tick> P|R <keycode>      (P = keyPressed, R = keyReleased)
 *                      <tick> T <aO> <aM>        (teleport player, fixed-point units)
 *                      <tick> S <index>          (bonus mode: pick list entry 0=main game, 1=bonus game)
 */
public class Trace {
    static Object get(Object o, String f) throws Exception {
        Field fl = o.getClass().getDeclaredField(f);
        fl.setAccessible(true);
        return fl.get(o);
    }

    static void set(Object o, String f, Object v) throws Exception {
        Field fl = o.getClass().getDeclaredField(f);
        fl.setAccessible(true);
        fl.set(o, v);
    }

    static Object call(Object o, String name, Class<?>[] types, Object... args) throws Exception {
        Method m = o.getClass().getDeclaredMethod(name, types);
        m.setAccessible(true);
        return m.invoke(o, args);
    }

    static final Class<?>[] II = {int.class, int.class};

    static String arr(Object a) {
        StringBuilder sb = new StringBuilder();
        int n = Array.getLength(a);
        for (int i = 0; i < n; i++) { if (i > 0) sb.append(','); sb.append(Array.get(a, i)); }
        return sb.toString();
    }

    static int hash(byte[] b) {
        int h = 17;
        for (byte x : b) h = h * 31 + x;
        return h;
    }

    static int hash(short[] b) {
        int h = 17;
        for (short x : b) h = h * 31 + x;
        return h;
    }

    public static void main(String[] a) throws Exception {
        String mode = a[0];
        long seed = Long.parseLong(a[1]);
        int ticks = Integer.parseInt(a[2]);
        Map<Integer, java.util.List<String[]>> script = new HashMap<>();
        if (!a[3].equals("-")) {
            try (BufferedReader r = new BufferedReader(new FileReader(a[3]))) {
                String ln;
                while ((ln = r.readLine()) != null) {
                    ln = ln.trim();
                    if (ln.isEmpty() || ln.startsWith("#")) continue;
                    String[] p = ln.split("\\s+");
                    script.computeIfAbsent(Integer.parseInt(p[0]), k -> new ArrayList<>()).add(p);
                }
            }
        }
        int startArea = a.length > 4 ? Integer.parseInt(a[4]) : Integer.MIN_VALUE;

        Object midlet = Class.forName("GameMIDlet").getDeclaredConstructor().newInstance();
        Object d = get(midlet, "new");
        set(d, "aD", new Random(seed));
        if (mode.equals("bonus")) ((byte[]) get(d, "E"))[0] = 1;
        call(d, "goto", new Class<?>[0]);              // start game (cutscene, or bonus menu)
        if (startArea != Integer.MIN_VALUE) {          // state injection: jump straight to an area
            call(d, "for", new Class<?>[]{int.class}, startArea);
            if (startArea >= 2) set(d, "cK", 0);       // sub-areas are normally entered from the playing state
        }

        java.util.Set<Integer> frames = new java.util.HashSet<>();
        String fs = System.getProperty("frames");
        if (fs != null) for (String f : fs.split(",")) if (!f.isEmpty()) frames.add(Integer.parseInt(f));
        String frameDir = System.getProperty("frameDir", ".");
        PrintStream out = System.out;
        StringBuilder k = new StringBuilder("#consts");
        for (String f : new String[]{"bT", "b6", "cl", "ck", "aP", "a0", "bZ", "cU", "C", "aw", "ah", "cr", "be", "bO", "c7", "bq", "bo", "at", "cP", "cD", "bV", "cd", "bY", "b3", "cz"})
            k.append(' ').append(f).append('=').append(get(d, f));
        k.append(" S=").append(arr(get(d, "S")));
        out.println(k);
        out.println(header());
        out.println(line(d, -1));
        for (int t = 0; t < ticks; t++) {
            java.util.List<String[]> ev = script.get(t);
            if (ev != null) for (String[] e : ev) {
                if (e[1].equals("T")) {          // teleport: set fixed-point player position directly
                    set(d, "aO", Integer.parseInt(e[2]));
                    set(d, "aM", Integer.parseInt(e[3]));
                } else if (e[1].equals("S")) {
                    ((javax.microedition.lcdui.List) get(d, "x")).selected = Integer.parseInt(e[2]);
                    ((CommandListener) d).commandAction(javax.microedition.lcdui.List.SELECT_COMMAND, (Displayable) get(d, "x"));
                } else {
                    int key = Integer.parseInt(e[2]);
                    int act = ((Canvas) d).getGameAction(key);
                    if (e[1].equals("P")) call(d, "a", II, key, act);
                    else call(d, "if", II, key, act);
                }
            }
            try {
                call(d, "do", II, 20, 0);
            } catch (InvocationTargetException ex) {      // original bug (e.g. falling below the map): record and stop
                out.println("#EXCEPTION tick=" + t + " " + ex.getCause());
                if (System.getenv("TRACE_STACK") != null) ex.getCause().printStackTrace();
                return;
            }
            out.println(line(d, t));
            if (frames.contains(t)) dumpFrame(d, frameDir, t);
        }
    }

    static void dumpFrame(Object d, String dir, int tick) throws Exception {
        Image screen = Image.createImage(176, 144);
        Graphics g = screen.getGraphics();
        call(d, "a", new Class<?>[]{Graphics.class}, g);
        java.awt.image.BufferedImage b = screen.awt();
        byte[] rgb = new byte[176 * 144 * 3];
        int k = 0;
        for (int y = 0; y < 144; y++) for (int x = 0; x < 176; x++) {
            int p = b.getRGB(x, y);
            rgb[k++] = (byte) (p >> 16); rgb[k++] = (byte) (p >> 8); rgb[k++] = (byte) p;
        }
        try (FileOutputStream o = new FileOutputStream(dir + "/frame_" + tick + ".rgb")) { o.write(rgb); }
        javax.imageio.ImageIO.write(b, "png", new File(dir + "/frame_" + tick + ".png"));
    }

    static String header() {
        return "tick cK c2 n cv aO aM am ak a1 a8 H a4 cO W bU cQ b1 E0 ci proj platHash mapHash grid";
    }

    static String line(Object d, int t) throws Exception {
        byte[] map = null;
        Object bd = get(d, "bd");
        for (Field f : bd.getClass().getDeclaredFields()) {
            if (f.getType() == byte[].class) { f.setAccessible(true); map = (byte[]) f.get(bd); break; }
        }
        short[] bH = (short[]) get(d, "bH");
        byte[] by = (byte[]) get(d, "by");
        StringBuilder proj = new StringBuilder();
        for (int i = 0; i < 3; i++) proj.append(by[i]).append(':').append(bH[i * 2]).append(':').append(bH[i * 2 + 1]).append(';');
        short[] cG = (short[]) get(d, "cG");
        byte[] grid = (byte[]) get(d, "cB");
        return t + " " + get(d, "cK") + " " + get(d, "c2") + " " + get(d, "n") + " " + get(d, "cv") + " "
                + get(d, "aO") + " " + get(d, "aM") + " " + get(d, "am") + " " + get(d, "ak") + " "
                + get(d, "a1") + " " + get(d, "a8") + " " + get(d, "H") + " " + get(d, "a4") + " " + get(d, "cO") + " "
                + get(d, "W") + " " + get(d, "bU") + " " + get(d, "cQ") + " " + get(d, "b1") + " "
                + ((byte[]) get(d, "E"))[0] + " " + get(d, "ci") + " " + proj + " "
                + (cG == null ? 0 : hash(cG)) + " " + (map == null ? 0 : hash(map)) + " " + (grid == null ? "-" : arr(grid));
    }
}
