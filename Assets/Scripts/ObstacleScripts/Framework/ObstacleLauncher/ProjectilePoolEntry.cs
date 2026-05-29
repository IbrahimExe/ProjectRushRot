using System;
using UnityEngine;

[Serializable]
public class ProjectilePoolEntry
{
    public Projectile prefab;
    public int defaultCapacity = 5;
    public int maxSize = 20;
}