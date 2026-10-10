namespace MikisWorld.Core;

/// <summary>Swept tile collision. The two games have their own implementation (<see cref="TileCollision"/>, <see cref="TileCollision2"/>)
/// because the originals differ in the details; the shared code only needs this surface.</summary>
public interface ITileCollision
{
    int Shift { get; }
    int OutDx { get; }
    int OutDy { get; }
    void Setup(int[] map, int mapW, int mapH, int tileW, int tileH, int[] flags);
    void SetShift(int s);
    bool Move(int x, int y, int w, int h, int dx, int dy);
}
