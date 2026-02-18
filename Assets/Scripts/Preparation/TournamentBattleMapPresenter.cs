using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;

namespace SevenBattles.Preparation
{
    public sealed class TournamentBattleMapPresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TournamentDefinition _tournament;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _battleRoot;
        [SerializeField] private PlayerContext _playerContext;

        [Header("Ellipse Rendering")]
        [SerializeField, Range(12, 128)] private int _segments = 48;
        [SerializeField] private float _outlineWidth = 0.04f;
        [SerializeField] private float _hoverWidth = 0.08f;
        [SerializeField] private Color _outlineColor = new Color(0.9f, 0.8f, 0.6f, 0.65f);
        [SerializeField] private Color _hoverColor = new Color(1f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color _hoverOtherColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        [SerializeField] private Color _completedColor = new Color(0.35f, 0.85f, 0.45f, 0.95f);
        [SerializeField] private float _hoverSpeed = 10f;
        [SerializeField] private int _sortingOrder = 1;
        [SerializeField] private float _ellipseZOffset = -0.05f;
        [SerializeField] private Material _lineMaterial;
        [SerializeField, Min(1)] private int _currentRoundIndex = 1;

        [Header("Cursor")]
        [SerializeField, Tooltip("Default cursor texture used in the preparation scene.")]
        private Texture2D _defaultCursorTexture;
        [SerializeField, Tooltip("Hotspot for the default cursor texture.")]
        private Vector2 _defaultCursorHotspot = new Vector2(4f, 4f);
        [SerializeField, Tooltip("Cursor texture shown when hovering the next battle ellipse.")]
        private Texture2D _nextBattleCursorTexture;
        [SerializeField, Tooltip("Hotspot for the next battle cursor texture.")]
        private Vector2 _nextBattleCursorHotspot = new Vector2(16f, 16f);

        private readonly List<BattleView> _views = new List<BattleView>();
        private Material _runtimeLineMaterial;
        private bool _interactionsEnabled = true;
        private bool _cursorActive;
        private bool _defaultCursorApplied;
        private bool _progressSubscribed;
        private int _lastHoveredIndex = -1;
        private TournamentBattleDefinition _lastHoveredDefinition;

        public event Action<TournamentBattleDefinition, int> BattleClicked;
        public event Action<TournamentBattleDefinition, int> BattleHoverChanged;

        public TournamentDefinition TournamentDefinition => _tournament;
        public int CurrentRoundIndex => ResolveCurrentRoundIndex();

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            ResolvePlayerContext();
            SubscribeProgress();
            BuildViews();
            ApplyDefaultCursor();
            ClearHoverState();
        }

        private void OnDisable()
        {
            UnsubscribeProgress();
            ClearViews();
            ApplyDefaultCursor();
            ClearHoverState();
        }

        private void OnDestroy()
        {
            if (_runtimeLineMaterial != null)
            {
                Destroy(_runtimeLineMaterial);
                _runtimeLineMaterial = null;
            }

            ApplyDefaultCursor();
        }

        private void Update()
        {
            if (!_interactionsEnabled || _camera == null || _battleRoot == null || _views.Count == 0)
            {
                ApplyDefaultCursor();
                ClearHoverState();
                return;
            }

            int currentRoundIndex = ResolveCurrentRoundIndex();
            var screen = Input.mousePosition;
            var toPlane = _battleRoot.position - _camera.transform.position;
            screen.z = Vector3.Dot(toPlane, _camera.transform.forward);
            var world = _camera.ScreenToWorldPoint(screen);
            var local = _battleRoot.InverseTransformPoint(world);
            var localPoint = new Vector2(local.x, local.y);

            BattleView hoveredSelectableView = null;
            BattleView hoveredInfoView = null;

            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                bool containsPoint = view.Definition != null &&
                                     view.Definition.Ellipse.ContainsPoint(localPoint);
                if (containsPoint && hoveredInfoView == null)
                {
                    hoveredInfoView = view;
                }

                bool hoveredSelectable = containsPoint && IsBattleSelectable(view, currentRoundIndex);
                float target = hoveredSelectable ? 1f : 0f;
                view.Hover = Mathf.MoveTowards(view.Hover, target, _hoverSpeed * Time.deltaTime);
                ApplyHover(view, currentRoundIndex);
                if (hoveredSelectable)
                {
                    hoveredSelectableView = view;
                }
            }

            bool isNextBattleHovered = hoveredSelectableView != null && hoveredSelectableView.Index == currentRoundIndex;
            UpdateHoverCursor(isNextBattleHovered);
            UpdateHoverState(hoveredInfoView);

            if (hoveredSelectableView != null && Input.GetMouseButtonDown(0))
            {
                BattleClicked?.Invoke(hoveredSelectableView.Definition, hoveredSelectableView.Index);
            }
        }

        public void SetTournamentDefinition(TournamentDefinition tournament)
        {
            _tournament = tournament;
            BuildViews();
        }

        public void SetCurrentRoundIndex(int roundIndex)
        {
            _currentRoundIndex = Mathf.Max(1, roundIndex);
        }

        public void SetInteractionsEnabled(bool enabled)
        {
            _interactionsEnabled = enabled;

            if (!_interactionsEnabled)
            {
                int currentRoundIndex = ResolveCurrentRoundIndex();
                for (int i = 0; i < _views.Count; i++)
                {
                    var view = _views[i];
                    view.Hover = 0f;
                    ApplyHover(view, currentRoundIndex);
                }

                ApplyDefaultCursor();
                ClearHoverState();
            }
        }

        private void UpdateHoverCursor(bool show)
        {
            if (_nextBattleCursorTexture == null)
            {
                ApplyDefaultCursor();
                return;
            }

            if (show == _cursorActive)
            {
                if (!show && !_defaultCursorApplied)
                {
                    ApplyDefaultCursor();
                }
                return;
            }

            _cursorActive = show;
            _defaultCursorApplied = false;
            if (show)
            {
                Cursor.SetCursor(_nextBattleCursorTexture, _nextBattleCursorHotspot, CursorMode.Auto);
            }
            else
            {
                ApplyDefaultCursor();
            }
        }

        private void ApplyDefaultCursor()
        {
            if (!_cursorActive && _defaultCursorApplied)
            {
                Cursor.SetCursor(_defaultCursorTexture, _defaultCursorHotspot, CursorMode.Auto);
                return;
            }

            _cursorActive = false;
            _defaultCursorApplied = true;
            Cursor.SetCursor(_defaultCursorTexture, _defaultCursorHotspot, CursorMode.Auto);
        }

        private void UpdateHoverState(BattleView hoveredView)
        {
            int hoveredIndex = hoveredView != null ? hoveredView.Index : -1;
            var hoveredDefinition = hoveredView != null ? hoveredView.Definition : null;

            if (hoveredIndex == _lastHoveredIndex && ReferenceEquals(hoveredDefinition, _lastHoveredDefinition))
            {
                return;
            }

            _lastHoveredIndex = hoveredIndex;
            _lastHoveredDefinition = hoveredDefinition;
            BattleHoverChanged?.Invoke(hoveredDefinition, hoveredIndex);
        }

        private void ClearHoverState()
        {
            if (_lastHoveredIndex == -1 && _lastHoveredDefinition == null)
            {
                return;
            }

            _lastHoveredIndex = -1;
            _lastHoveredDefinition = null;
            BattleHoverChanged?.Invoke(null, -1);
        }

        private void EnsureReferences()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_battleRoot == null)
            {
                _battleRoot = transform;
            }

            ResolvePlayerContext();
        }

        private void BuildViews()
        {
            ClearViews();
            EnsureReferences();

            if (_tournament == null || _battleRoot == null)
            {
                return;
            }

            var battles = _tournament.Battles;
            if (battles == null)
            {
                return;
            }

            var material = ResolveLineMaterial();
            int count = battles.Length;
            int currentRoundIndex = ResolveCurrentRoundIndex();

            for (int i = 0; i < count; i++)
            {
                var battle = battles[i];
                if (battle == null)
                {
                    continue;
                }

                var ellipse = battle.Ellipse;
                var root = new GameObject($"TournamentBattle_{i + 1}");
                root.transform.SetParent(_battleRoot, false);
                root.transform.localPosition = new Vector3(ellipse.Center.x, ellipse.Center.y, _ellipseZOffset);

                var line = root.AddComponent<LineRenderer>();
                ConfigureLineRenderer(line, material);
                BuildEllipseLine(line, ellipse);

                bool isCompleted = IsBattleCompleted(i + 1);
                if (!isCompleted && battle.EnemyPrefab != null)
                {
                    var enemy = Instantiate(battle.EnemyPrefab, root.transform);
                    enemy.transform.localPosition = new Vector3(battle.PrefabOffset.x, battle.PrefabOffset.y, 0f);
                    enemy.transform.localScale = Vector3.one * battle.PrefabScale;
                    SetDownFacingIfCharacter4D(enemy);
                }

                var view = new BattleView(root.transform, line, battle, i + 1);
                _views.Add(view);
                ApplyHover(view, currentRoundIndex);
            }
        }

        private void ClearViews()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view.Root != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(view.Root.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(view.Root.gameObject);
                    }
                }
            }

            _views.Clear();
        }

        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null)
            {
                return _lineMaterial;
            }

            if (_runtimeLineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _runtimeLineMaterial = new Material(shader) { color = Color.white };
            }

            return _runtimeLineMaterial;
        }

        private void ConfigureLineRenderer(LineRenderer line, Material material)
        {
            line.useWorldSpace = false;
            line.loop = true;
            line.sharedMaterial = material;
            line.startWidth = _outlineWidth;
            line.endWidth = _outlineWidth;
            line.startColor = _outlineColor;
            line.endColor = _outlineColor;
            line.sortingOrder = _sortingOrder;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
        }

        private void BuildEllipseLine(LineRenderer line, EllipseDefinition ellipse)
        {
            int steps = Mathf.Max(12, _segments);
            line.positionCount = steps;
            float step = Mathf.PI * 2f / steps;

            for (int i = 0; i < steps; i++)
            {
                float angle = step * i;
                var point = ellipse.GetPointOnPerimeter(angle);
                line.SetPosition(i, new Vector3(point.x, point.y, 0f));
            }
        }

        private void ApplyHover(BattleView view, int currentRoundIndex)
        {
            if (view.Line == null)
            {
                return;
            }

            if (IsBattleCompleted(view.Index))
            {
                view.Line.startColor = _completedColor;
                view.Line.endColor = _completedColor;
                view.Line.startWidth = _outlineWidth;
                view.Line.endWidth = _outlineWidth;
                return;
            }

            var targetColor = view.Index == currentRoundIndex ? _hoverColor : _hoverOtherColor;
            var color = Color.Lerp(_outlineColor, targetColor, view.Hover);
            float width = Mathf.Lerp(_outlineWidth, _hoverWidth, view.Hover);
            view.Line.startColor = color;
            view.Line.endColor = color;
            view.Line.startWidth = width;
            view.Line.endWidth = width;
        }

        private int GetClampedRoundIndex()
        {
            int maxRound = _views.Count;
            if (maxRound == 0 && _tournament != null && _tournament.Battles != null)
            {
                maxRound = _tournament.Battles.Length;
            }

            if (maxRound <= 0)
            {
                return 1;
            }

            return Mathf.Clamp(_currentRoundIndex, 1, maxRound);
        }

        private int ResolveCurrentRoundIndex()
        {
            if (_playerContext != null)
            {
                int maxRound = _views.Count > 0 ? _views.Count : (_tournament != null && _tournament.Battles != null ? _tournament.Battles.Length : 1);
                return Mathf.Clamp(_playerContext.CurrentTournamentRoundIndex, 1, Mathf.Max(1, maxRound));
            }

            return GetClampedRoundIndex();
        }

        private bool IsBattleCompleted(int roundIndex)
        {
            return _playerContext != null && _playerContext.IsTournamentBattleCompleted(roundIndex);
        }

        private bool IsBattleSelectable(BattleView view, int currentRoundIndex)
        {
            if (view == null)
            {
                return false;
            }

            return !IsBattleCompleted(view.Index) && view.Index == currentRoundIndex;
        }

        private void ResolvePlayerContext()
        {
            if (_playerContext != null)
            {
                return;
            }

            var contexts = Resources.FindObjectsOfTypeAll<PlayerContext>();
            for (int i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] != null)
                {
                    _playerContext = contexts[i];
                    return;
                }
            }
        }

        private void SubscribeProgress()
        {
            if (_progressSubscribed || _playerContext == null)
            {
                return;
            }

            _playerContext.TournamentProgressChanged += HandleTournamentProgressChanged;
            _progressSubscribed = true;
        }

        private void UnsubscribeProgress()
        {
            if (!_progressSubscribed || _playerContext == null)
            {
                return;
            }

            _playerContext.TournamentProgressChanged -= HandleTournamentProgressChanged;
            _progressSubscribed = false;
        }

        private void HandleTournamentProgressChanged()
        {
            BuildViews();
        }

        private static void SetDownFacingIfCharacter4D(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            try
            {
                var components = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    var comp = components[i];
                    if (comp == null) continue;
                    var type = comp.GetType();
                    if (type.Name != "Character4D" &&
                        type.FullName != "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D")
                    {
                        continue;
                    }

                    var method = type.GetMethod(
                        "SetDirection",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new System.Type[] { typeof(Vector2) },
                        null);
                    if (method == null)
                    {
                        return;
                    }

                    method.Invoke(comp, new object[] { Vector2.down });
                    return;
                }
            }
            catch
            {
                // Ignore missing reflection targets for non-HeroEditor prefabs.
            }
        }

        private sealed class BattleView
        {
            public readonly Transform Root;
            public readonly LineRenderer Line;
            public readonly TournamentBattleDefinition Definition;
            public readonly int Index;
            public float Hover;

            public BattleView(Transform root, LineRenderer line, TournamentBattleDefinition definition, int index)
            {
                Root = root;
                Line = line;
                Definition = definition;
                Index = index;
                Hover = 0f;
            }
        }
    }
}
