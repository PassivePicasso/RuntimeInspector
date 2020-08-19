using System;
using System.Linq;
using UnityEngine;

namespace PassivePicasso.RuntimeInspector
{
    [RequireComponent(typeof(RectTransform)), ExecuteAlways]
    public class Foldout : MonoBehaviour
    {
        public enum Side { Top, Left, Right, Bottom }
        public bool Open;
        public bool Pinned;
        public float maxSize;
        public float minSize;
        public Side side;
        public KeyCode Key;
        public KeyCode Modifier;
        public Transform Thumb;

        RectTransform rectTransform;
        private float CurrentSize => Open ? maxSize : minSize;

        void Start()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(Key) && Input.GetKey(Modifier))
                Pinned = !Pinned;

            Open = Input.GetKey(Key) || Pinned;

            UpdateDimensions();

            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i)?.gameObject?.SetActive(Open);
        }

        private void UpdateDimensions()
        {
            switch (side)
            {
                case Side.Top:
                    rectTransform.anchorMin = Vector2.up;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.pivot = new Vector2(0, 1);
                    rectTransform.sizeDelta = new Vector2(0, CurrentSize);
                    rectTransform.anchoredPosition = Vector3.zero;
                    break;
                case Side.Left:
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.up;
                    rectTransform.pivot = new Vector2(0, 0.5f);
                    rectTransform.anchoredPosition = Vector3.zero;
                    rectTransform.sizeDelta = new Vector2(CurrentSize, 0);
                    break;
                case Side.Right:
                    rectTransform.anchorMin = Vector2.right;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.pivot = new Vector2(1, 0.5f);
                    rectTransform.anchoredPosition = Vector3.zero;
                    rectTransform.sizeDelta = new Vector2(CurrentSize, 0);
                    break;
                case Side.Bottom:
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.right;
                    rectTransform.pivot = new Vector2(0.5f, 0);
                    rectTransform.anchoredPosition = Vector3.zero;
                    rectTransform.sizeDelta = new Vector2(0, CurrentSize);
                    break;
            }
        }
    }
}