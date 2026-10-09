package javax.microedition.rms;
import java.util.*;
public class RecordStore {
    private static final Map<String, List<byte[]>> DB = new HashMap<>();
    private final List<byte[]> recs;
    private RecordStore(List<byte[]> r) { recs = r; }
    public static RecordStore openRecordStore(String n, boolean create) throws RecordStoreException {
        List<byte[]> r = DB.get(n);
        if (r == null) { if (!create) throw new RecordStoreException(n); r = new ArrayList<>(); DB.put(n, r); }
        return new RecordStore(r);
    }
    public static void deleteRecordStore(String n) { DB.remove(n); }
    public int getNumRecords() { return recs.size(); }
    public int addRecord(byte[] d, int o, int l) { recs.add(Arrays.copyOfRange(d, o, o + l)); return recs.size(); }
    public byte[] getRecord(int id) { return recs.get(id - 1).clone(); }
    public void setRecord(int id, byte[] d, int o, int l) { recs.set(id - 1, Arrays.copyOfRange(d, o, o + l)); }
    public void closeRecordStore() {}
}
