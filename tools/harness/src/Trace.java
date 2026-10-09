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
    /** Logical name -> real (obfuscated) name. Empty for MikisWorld 1; see tools/harness/names_mw2.properties. */
    static final Properties N = new Properties();

    static String nm(String logical) { return N.getProperty(logical, logical); }

    static boolean flag(String k) { return Boolean.parseBoolean(N.getProperty(k, "false")); }

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
        String namesFile = System.getProperty("names");
        if (namesFile != null) try (FileReader fr = new FileReader(namesFile)) { N.load(fr); }
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
        Object d = get(midlet, nm("new"));
        set(d, nm("aD"), new Random(seed));
        if (mode.equals("bonus")) ((byte[]) get(d, nm("E")))[0] = 1;
        call(d, nm("goto"), new Class<?>[0]);              // start game (cutscene, or bonus menu)
        if (startArea != Integer.MIN_VALUE) {          // state injection: jump straight to an area
            call(d, nm("for"), new Class<?>[]{int.class}, startArea);
            if (startArea >= 2) set(d, nm("cK"), 0);       // sub-areas are normally entered from the playing state
        }

        java.util.Set<Integer> frames = new java.util.HashSet<>();
        String fs = System.getProperty("frames");
        if (fs != null) for (String f : fs.split(",")) if (!f.isEmpty()) frames.add(Integer.parseInt(f));
        String frameDir = System.getProperty("frameDir", ".");
        int keepLives = Integer.getInteger("keeplives", 0);
        PrintStream out = System.out;
        StringBuilder k = new StringBuilder("#consts");
        String[] consts = N.getProperty("consts", "bT,b6,cl,ck,aP,a0,bZ,cU,C,aw,ah,cr,be,bO,c7,bq,bo,at,cP,cD,bV,cd,bY,b3,cz").split(",");
        for (String f : consts) if (!f.isEmpty()) k.append(' ').append(f).append('=').append(get(d, nm(f)));
        String jt = N.getProperty("jumptable", "S");
        if (!jt.isEmpty()) k.append(" S=").append(arr(get(d, jt)));
        out.println(k);
        out.println(header());
        out.println(line(d, -1));
        for (int t = 0; t < ticks; t++) {
            java.util.List<String[]> ev = script.get(t);
            if (ev != null) for (String[] e : ev) {
                if (e[1].equals("T")) {          // teleport: set fixed-point player position directly
                    set(d, nm("aO"), Integer.parseInt(e[2]));
                    set(d, nm("aM"), Integer.parseInt(e[3]));
                } else if (e[1].equals("S")) {
                    ((javax.microedition.lcdui.List) get(d, nm("x"))).selected = Integer.parseInt(e[2]);
                    ((CommandListener) d).commandAction(javax.microedition.lcdui.List.SELECT_COMMAND, (Displayable) get(d, nm("x")));
                } else {
                    int key = Integer.parseInt(e[2]);
                    int act = ((Canvas) d).getGameAction(key);
                    if (e[1].equals("P")) call(d, nm("a"), II, key, act);
                    else call(d, nm("if"), II, key, act);
                }
            }
            if (keepLives > 0 && (Integer) get(d, nm("a8")) < 2 && (Integer) get(d, nm("cK")) != 3) set(d, nm("a8"), keepLives);
            try {
                call(d, nm("do"), II, 20, flag("tick.total") ? 20 * (t + 1) : 0);
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
        int sw = Integer.parseInt(N.getProperty("screen.w", "176")), sh = Integer.parseInt(N.getProperty("screen.h", "144"));
        Image screen = Image.createImage(sw, sh);
        Graphics g = screen.getGraphics();
        call(d, "a", new Class<?>[]{Graphics.class}, g);
        java.awt.image.BufferedImage b = screen.awt();
        byte[] rgb = new byte[sw * sh * 3];
        int k = 0;
        for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++) {
            int p = b.getRGB(x, y);
            rgb[k++] = (byte) (p >> 16); rgb[k++] = (byte) (p >> 8); rgb[k++] = (byte) p;
        }
        try (FileOutputStream o = new FileOutputStream(dir + "/frame_" + tick + ".rgb")) { o.write(rgb); }
        javax.imageio.ImageIO.write(b, "png", new File(dir + "/frame_" + tick + ".png"));
    }

    static String header() {
        return "tick cK c2 n cv aO aM am ak a1 a8 H a4 cO W bU cQ b1 E0 ci proj platHash mapHash grid"
                + (N.getProperty("extra", "").isEmpty() ? "" : " " + N.getProperty("extra").replace(',', ' '));
    }

    static String line(Object d, int t) throws Exception {
        byte[] map = null;
        Object bd = get(d, nm("bd"));
        for (Field f : bd.getClass().getDeclaredFields()) {
            if (f.getType() == byte[].class) { f.setAccessible(true); map = (byte[]) f.get(bd); break; }
        }
        short[] bH = (short[]) get(d, nm("bH"));
        byte[] by = (byte[]) get(d, nm("by"));
        StringBuilder proj = new StringBuilder();
        if (by == null || bH == null) proj.append('-');
        else for (int i = 0; i < 3; i++) proj.append(by[i]).append(':').append(bH[i * 2]).append(':').append(bH[i * 2 + 1]).append(';');
        short[] cG = (short[]) get(d, nm("cG"));
        byte[] grid = (byte[]) get(d, nm("cB"));
        StringBuilder extra = new StringBuilder();
        for (String f : N.getProperty("extra", "").split(",")) {
            if (f.isEmpty()) continue;
            Object v = get(d, f);
            extra.append(' ').append(v != null && v.getClass().isArray() ? arr(v) : String.valueOf(v));
        }
        return t + " " + get(d, nm("cK")) + " " + get(d, nm("c2")) + " " + get(d, nm("n")) + " " + get(d, nm("cv")) + " "
                + get(d, nm("aO")) + " " + get(d, nm("aM")) + " " + get(d, nm("am")) + " " + get(d, nm("ak")) + " "
                + get(d, nm("a1")) + " " + get(d, nm("a8")) + " " + get(d, nm("H")) + " " + get(d, nm("a4")) + " " + get(d, nm("cO")) + " "
                + get(d, nm("W")) + " " + get(d, nm("bU")) + " " + get(d, nm("cQ")) + " " + get(d, nm("b1")) + " "
                + ((byte[]) get(d, nm("E")))[0] + " " + get(d, nm("ci")) + " " + proj + " "
                + (cG == null ? 0 : hash(cG)) + " " + (map == null ? 0 : hash(map)) + " " + (grid == null ? "-" : arr(grid)) + extra;
    }
}
