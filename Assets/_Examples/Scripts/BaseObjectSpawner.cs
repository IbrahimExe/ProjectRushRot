using UnityEngine;

namespace Examples
{

    public abstract class BaseObjectSpawner : MonoBehaviour
    {
        public abstract void Initialize();
        public abstract void StartSpawning();
    }

}
