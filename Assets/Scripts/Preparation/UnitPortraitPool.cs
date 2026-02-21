using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenBattles.Preparation
{
    public sealed class UnitPortraitPool
    {
        private readonly UnitPortraitView _prefab;
        private readonly Transform _parent;
        private readonly Stack<UnitPortraitView> _pool;
        private readonly List<UnitPortraitView> _active;

        public UnitPortraitPool(UnitPortraitView prefab, Transform parent, int initialSize)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            _prefab = prefab;
            _parent = parent;
            int size = System.Math.Max(0, initialSize);
            _pool = new Stack<UnitPortraitView>(size);
            _active = new List<UnitPortraitView>(size);

            for (int i = 0; i < size; i++)
            {
                UnitPortraitView view = CreateInstance();
                view.gameObject.SetActive(false);
                _pool.Push(view);
            }
        }

        public UnitPortraitView Get()
        {
            UnitPortraitView view = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
            _active.Add(view);
            view.gameObject.SetActive(true);
            return view;
        }

        public void Return(UnitPortraitView view)
        {
            if (view == null)
            {
                return;
            }

            if (!_active.Remove(view))
            {
                return;
            }

            if (_parent != null)
            {
                view.transform.SetParent(_parent, false);
            }

            view.Clear();
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        public void ReturnAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Return(_active[i]);
            }
        }

        private UnitPortraitView CreateInstance()
        {
            return UnityEngine.Object.Instantiate(_prefab, _parent);
        }
    }
}
