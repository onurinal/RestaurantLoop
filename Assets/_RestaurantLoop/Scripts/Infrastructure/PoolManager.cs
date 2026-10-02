using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DG.Tweening;

namespace RestaurantLoop.Infrastructure
{
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private Dictionary<GameObject, ObjectPool<GameObject>> pools = new Dictionary<GameObject, ObjectPool<GameObject>>();
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
                    actionOnRelease: (obj) =>
                    {
                        obj.SetActive(false);
                        obj.transform.SetParent(transform);
                    },
                    actionOnDestroy: (obj) => Destroy(obj),
                    collectionCheck: false,
                    defaultCapacity: 10,
                    // The largest supported level has 120 customers. Keep enough
                    // instances for a single prefab type to avoid level-to-level
                    // churn in that worst case.
                    maxSize: 128
                );
            }

            GameObject instance = pools[prefab].Get();
            instance.transform.DOKill();
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

            // A stack can be referenced by more than one gameplay container while
            // a level is being cleared. Releasing an already-pooled instance must
            // be harmless instead of destroying it on the second cleanup pass.
            if (!instance.activeSelf && instance.transform.parent == transform)
            {
                return;
            }

            instance.transform.DOKill();

            if (instanceToPrefabMap.TryGetValue(instance, out GameObject prefab))
            {
                if (pools.ContainsKey(prefab))
                {
                    pools[prefab].Release(instance);
                    instanceToPrefabMap.Remove(instance);
                    return;
                }
            }

            Destroy(instance);
        }
    }
}
