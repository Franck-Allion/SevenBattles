using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle;
using SevenBattles.Core;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Battle
{
    public class BattlefieldServiceTests
    {
        private sealed class FakeSessionService : IBattleSessionService
        {
            public BattleSessionConfig CurrentSession { get; set; }
            public void InitializeSession(BattleSessionConfig config) => CurrentSession = config;
            public void ClearSession() => CurrentSession = null;
        }

        private sealed class SessionServiceProxy : MonoBehaviour, IBattleSessionService
        {
            public IBattleSessionService Inner;
            public BattleSessionConfig CurrentSession => Inner?.CurrentSession;
            public void InitializeSession(BattleSessionConfig config) => Inner?.InitializeSession(config);
            public void ClearSession() => Inner?.ClearSession();
        }

        [Test]
        public void RefreshBattlefield_UsesSessionBattlefieldDefinition()
        {
            var session = new FakeSessionService();
            var battlefield = ScriptableObject.CreateInstance<BattlefieldDefinition>();
            battlefield.Id = "bf.direct";

            session.CurrentSession = new BattleSessionConfig
            {
                Battlefield = battlefield
            };

            var go = new GameObject("BattlefieldServiceTest");
            var proxy = go.AddComponent<SessionServiceProxy>();
            proxy.Inner = session;

            var service = go.AddComponent<BattlefieldService>();
            SetPrivateField(service, "_sessionServiceBehaviour", proxy);

            service.RefreshBattlefield();

            Assert.AreSame(battlefield, service.Current);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RefreshBattlefield_ResolvesByIdFromRegistry()
        {
            var session = new FakeSessionService();
            session.CurrentSession = new BattleSessionConfig
            {
                BattlefieldId = "bf.registry"
            };

            var registry = ScriptableObject.CreateInstance<BattlefieldDefinitionRegistry>();
            var battlefield = ScriptableObject.CreateInstance<BattlefieldDefinition>();
            battlefield.Id = "bf.registry";
            SetPrivateField(registry, "_definitions", new[] { battlefield });

            var go = new GameObject("BattlefieldServiceTestRegistry");
            var proxy = go.AddComponent<SessionServiceProxy>();
            proxy.Inner = session;

            var service = go.AddComponent<BattlefieldService>();
            SetPrivateField(service, "_sessionServiceBehaviour", proxy);
            SetPrivateField(service, "_registry", registry);

            service.RefreshBattlefield();

            Assert.AreSame(battlefield, service.Current);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RefreshBattlefield_FallsBackToInspectorDefault_WhenIdMissing()
        {
            var session = new FakeSessionService();
            session.CurrentSession = new BattleSessionConfig
            {
                BattlefieldId = "bf.missing"
            };

            var fallback = ScriptableObject.CreateInstance<BattlefieldDefinition>();
            fallback.Id = "bf.fallback";

            var go = new GameObject("BattlefieldServiceTestFallback");
            var proxy = go.AddComponent<SessionServiceProxy>();
            proxy.Inner = session;

            var service = go.AddComponent<BattlefieldService>();
            SetPrivateField(service, "_sessionServiceBehaviour", proxy);
            SetPrivateField(service, "_inspectorDefaultBattlefield", fallback);

            service.RefreshBattlefield();

            Assert.AreSame(fallback, service.Current);
            Object.DestroyImmediate(go);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}
