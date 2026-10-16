using UnityEngine;

namespace Spieleabend.Core;

/// <summary>Hilfen fuer Dinge, die Rollen auf die Map stellen (Fallen, Laternen ...): Bild anzeigen und Position per RPC verschicken.</summary>
public static class WorldMarker
{
    /// <summary>Stellt ein Symbol (Name aus Assets/Icons) auf die Map. Haengt an der Map und verschwindet mit ihr.</summary>
    public static GameObject Spawn(string icon, Vector2 pos, float scale, string name)
    {
        var go = new GameObject("Spieleabend_" + name);
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f - 0.01f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ButtonIcons.Get(icon, null);
        return go;
    }

    /// <summary>Position als 4 Bytes (Hundertstel, reicht fuer alle Maps).</summary>
    public static byte[] Encode(Vector2 pos)
    {
        short x = (short)Mathf.RoundToInt(pos.x * 100f), y = (short)Mathf.RoundToInt(pos.y * 100f);
        return new[] { (byte)x, (byte)(x >> 8), (byte)y, (byte)(y >> 8) };
    }

    public static Vector2 Decode(byte[] data, int offset) =>
        new((short)(data[offset] | data[offset + 1] << 8) / 100f, (short)(data[offset + 2] | data[offset + 3] << 8) / 100f);

    public static byte[] Concat(byte first, byte[] rest)
    {
        var all = new byte[rest.Length + 1];
        all[0] = first;
        rest.CopyTo(all, 1);
        return all;
    }
}
