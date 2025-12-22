using System;
using UnityEngine;

namespace SevenBattles.Battle.Spells
{
    public sealed class SpellProjectileImpactRelay : MonoBehaviour
    {
        private Transform _expectedTargetRoot;
        private Action<bool, Vector3> _onImpact;
        private Action _onComplete;
        private bool _resolved;

        public void Initialize(GameObject expectedTargetRoot, Action<bool, Vector3> onImpact, Action onComplete)
        {
            _expectedTargetRoot = expectedTargetRoot != null ? expectedTargetRoot.transform : null;
            _onImpact = onImpact;
            _onComplete = onComplete;
            _resolved = false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_resolved) return;

            bool isTarget = false;
            if (_expectedTargetRoot != null && collision != null)
            {
                var collider = collision.collider;
                if (collider != null)
                {
                    isTarget = collider.transform != null && collider.transform.IsChildOf(_expectedTargetRoot);
                }
            }

            Vector3 impactPosition = transform.position;
            if (collision != null && collision.contactCount > 0)
            {
                impactPosition = collision.GetContact(0).point;
            }

            Resolve(isTarget, impactPosition);
        }

        private void OnDestroy()
        {
            if (_resolved) return;
            Resolve(false, transform.position);
        }

        private void Resolve(bool validHit, Vector3 impactPosition)
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                _onImpact?.Invoke(validHit, impactPosition);
            }
            finally
            {
                _onComplete?.Invoke();
            }
        }
    }
}

