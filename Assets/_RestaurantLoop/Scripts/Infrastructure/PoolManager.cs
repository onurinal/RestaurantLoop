using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace RestaurantLoop.Infrastructure
{
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private Dictionary<GameObject, ObjectPool<GameObject>> pools = new Dictionary<GameObject, ObjectPool<GameObject>>();
        
        // Maps spawned instances back to their original prefabs for accurate despawning
        private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;

            if (!pools.ContainsKey(prefab))
            {
                pools[prefab] = new ObjectPool<GameObject>(
                    createFunc: () => Instantiate(prefab),
                    actionOnGet: (obj) => { obj.SetActive(true); },
                    actionOnRelease: (obj) => { obj.SetActive(false); obj.transform.SetParent(transform); },
                    actionOnDestroy: (obj) => Destroy(obj),
                    collectionCheck: false,
                    defaultCapacity: 10,
                    maxSize: 100
                );
            }

            GameObject instance = pools[prefab].Get();
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            
            if (parent != null)
            {
                instance.transform.SetParent(parent);
            }

            instanceToPrefabMap[instance] = prefab;

            return instance;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (instanceToPrefabMap.TryGetValue(instance, out GameObject prefab))
            {
                if (pools.ContainsKey(prefab))
                {
                    pools[prefab].Release(instance);
                    instanceToPrefabMap.Remove(instance);
                    return;
                }
            }

            // Fallback: Destroy if the object was not created by the pool system
            Destroy(instance);
        }
    }
}