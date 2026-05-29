using System.Collections.Generic;

public static class PoolRegistry
{
    private static readonly Dictionary<string, ObjectPool> pools = new();

    public static void Register(string id, ObjectPool pool)
    {
        pools[id] = pool;
    }

    public static ObjectPool Get(string id)
    {
        pools.TryGetValue(id, out ObjectPool pool);
        return pool;
    }
}