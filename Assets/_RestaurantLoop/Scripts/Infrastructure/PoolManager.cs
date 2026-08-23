using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace RestaurantLoop.Core.Utilities
{
    /// <summary>
    /// Unity'nin yerleşik ObjectPool sistemini kullanan, over-engineering'den uzak temiz havuz yöneticisi.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        // Prefab'ı key olarak kullanarak her prefab tipi için ayrı bir havuz tutuyoruz
        private Dictionary<GameObject, ObjectPool<GameObject>> pools = new Dictionary<GameObject, ObjectPool<GameObject>>();
        
        // Sahnedeki klonların hangi prefab'dan üretildiğini bulmak için ters referans haritası
        private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Instantiate yerine kullanılır. Havuzda varsa getirir, yoksa yeni üretir.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;

            // Eğer bu prefab için daha önce havuz açılmadıysa, hemen temiz bir tane aç
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

            // Bu klonun hangi prefab'a ait olduğunu kaydediyoruz ki iade ederken (Despawn) bulalım
            instanceToPrefabMap[instance] = prefab;

            return instance;
        }

        /// <summary>
        /// Destroy yerine kullanılır. Objeyi silmez, kapatıp havuza geri gönderir.
        /// </summary>
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

            // Eğer obje bizim havuz sistemimizden çıkmadıysa (yanlışlıkla normal üretildiyse) güvenli şekilde sil
            Destroy(instance);
        }
    }
}