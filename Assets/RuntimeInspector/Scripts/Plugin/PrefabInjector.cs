using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PassivePicasso.RuntimeInspector
{
    [RequireComponent(typeof(RectTransform))]
    public class PrefabInjector : MonoBehaviour
    {
        public RectTransform prefab;

        private void Start()
        {
            var instance = Instantiate(prefab);
            instance.SetParent(GetComponent<RectTransform>(), false);
            instance.anchorMin = Vector2.zero;
            instance.anchorMax = Vector2.one;
            instance.pivot = new Vector2(0, 0);
            instance.sizeDelta = new Vector2(0, 0);
            instance.anchoredPosition = Vector3.zero;
        }
    }
}
