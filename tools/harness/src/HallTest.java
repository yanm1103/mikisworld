import java.lang.reflect.*;
import java.util.*;

/** Differential test for the original hall-of-fame class k: random inserts, prints the table after each step. */
public class HallTest {
    public static void main(String[] a) throws Exception {
        long seed = Long.parseLong(a[0]);
        int steps = Integer.parseInt(a[1]);
        Class<?> c = Class.forName("k");
        Constructor<?> ctor = c.getDeclaredConstructor(String.class, String.class, int.class, int.class, int.class, boolean.class);
        ctor.setAccessible(true);
        Object k = ctor.newInstance("HallOfFame", "aaa", 0, 1, 10, true);
        Method isHigh = c.getDeclaredMethod("do", int.class, int.class);
        Method insert = c.getDeclaredMethod("a", int.class, String.class, int.class);
        Method name = c.getDeclaredMethod("if", int.class, int.class);
        Method score = c.getDeclaredMethod("a", int.class, int.class);
        Random r = new Random(seed);
        String[] names = {"bob", "AMY", " ", null, "", "zed", "a b", "mike"};
        for (int i = 0; i < steps; i++) {
            String n = names[(r.nextInt() & 0x7fffffff) % names.length];
            int s = (r.nextInt() & 0x7fffffff) % 25;
            boolean high = (Boolean) isHigh.invoke(k, 0, s);
            if (high || (r.nextInt() & 1) == 0) insert.invoke(k, 0, n, s);
            StringBuilder sb = new StringBuilder().append(i).append(' ').append(s).append(' ').append(high).append(" |");
            for (int j = 0; j < 10; j++) sb.append(' ').append(name.invoke(k, 0, j)).append('=').append(score.invoke(k, 0, j));
            System.out.println(sb);
        }
    }
}
