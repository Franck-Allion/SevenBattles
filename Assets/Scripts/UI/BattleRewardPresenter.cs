using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using UnityEngine;

namespace SevenBattles.UI
{
    public sealed class BattleRewardPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _rewardItemPrefab;
        [SerializeField] private Transform _container;

        public void Show(BattleRewardResult result)
        {
            Clear();

            if (result == null)
            {
                return;
            }

            EnsureContainer();

            CreateRewardView(view => view.SetGold(result.GoldAmount));

            var bonusRewards = result.BonusRewards;
            if (bonusRewards == null || bonusRewards.Length == 0)
            {
                return;
            }

            for (int i = 0; i < bonusRewards.Length; i++)
            {
                BattleRewardResultEntry entry = bonusRewards[i];
                if (entry == null)
                {
                    continue;
                }

                CreateRewardView(view => view.SetReward(entry));
            }
        }

        public void Clear()
        {
            EnsureContainer();
            if (_container == null)
            {
                return;
            }

            for (int i = _container.childCount - 1; i >= 0; i--)
            {
                var child = _container.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                DestroyObject(child.gameObject);
            }
        }

        private void EnsureContainer()
        {
            if (_container == null)
            {
                _container = transform;
            }
        }

        private void CreateRewardView(System.Action<RewardItemView> configure)
        {
            if (_rewardItemPrefab == null)
            {
                SBLog.Warn("BattleRewardPresenter: Reward item prefab is not assigned.", this);
                return;
            }

            if (_container == null)
            {
                SBLog.Warn("BattleRewardPresenter: Reward container is not assigned.", this);
                return;
            }

            var instance = Instantiate(_rewardItemPrefab, _container);
            var view = instance.GetComponent<RewardItemView>();
            if (view == null)
            {
                view = instance.GetComponentInChildren<RewardItemView>(true);
            }

            if (view == null)
            {
                SBLog.Warn("BattleRewardPresenter: Reward item prefab is missing RewardItemView.", this);
                DestroyObject(instance);
                return;
            }

            configure?.Invoke(view);
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
