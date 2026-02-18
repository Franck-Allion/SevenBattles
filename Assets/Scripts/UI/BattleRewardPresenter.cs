using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using TMPro;
using UnityEngine;

namespace SevenBattles.UI
{
    public sealed class BattleRewardPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _rewardItemPrefab;
        [SerializeField] private Transform _container;
        private RewardItemView _goldRewardView;
        private RewardItemView _gemsRewardView;

        public void Show(BattleRewardResult result)
        {
            Clear();
            _goldRewardView = null;
            _gemsRewardView = null;

            if (result == null)
            {
                return;
            }

            EnsureContainer();

            _goldRewardView = CreateRewardView(view => view.SetGold(result.GoldAmount));

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

                var view = CreateRewardView(v => v.SetReward(entry));
                if (entry.Type == BattleRewardType.Gems && _gemsRewardView == null)
                {
                    _gemsRewardView = view;
                }
            }
        }

        public void Clear()
        {
            EnsureContainer();
            _goldRewardView = null;
            _gemsRewardView = null;
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

        public bool TryGetCurrencyAmountText(BattleRewardType type, out TMP_Text amountText, out RectTransform amountRectTransform)
        {
            amountText = null;
            amountRectTransform = null;

            var view = GetCurrencyView(type);
            if (view == null || view.AmountText == null || view.AmountRectTransform == null)
            {
                return false;
            }

            amountText = view.AmountText;
            amountRectTransform = view.AmountRectTransform;
            return true;
        }

        public void SetCurrencyAmountDisplay(BattleRewardType type, int amount)
        {
            var view = GetCurrencyView(type);
            if (view == null)
            {
                return;
            }

            view.SetCurrencyAmountDisplay(amount);
        }

        private RewardItemView GetCurrencyView(BattleRewardType type)
        {
            switch (type)
            {
                case BattleRewardType.Gold:
                    return _goldRewardView;
                case BattleRewardType.Gems:
                    return _gemsRewardView;
                default:
                    return null;
            }
        }

        private void EnsureContainer()
        {
            if (_container == null)
            {
                _container = transform;
            }
        }

        private RewardItemView CreateRewardView(System.Action<RewardItemView> configure)
        {
            if (_rewardItemPrefab == null)
            {
                SBLog.Warn("BattleRewardPresenter: Reward item prefab is not assigned.", this);
                return null;
            }

            if (_container == null)
            {
                SBLog.Warn("BattleRewardPresenter: Reward container is not assigned.", this);
                return null;
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
                return null;
            }

            configure?.Invoke(view);
            return view;
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
