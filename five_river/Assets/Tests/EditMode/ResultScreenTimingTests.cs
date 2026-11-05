using System.Reflection;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.Tests
{
    public class ResultScreenTimingTests
    {
        // 실제 ResultScreen.Update를 실행하여 날짜가 이름 구간에 남는 회귀를 잡는다.
        [TestCase(2f, 0)]
        [TestCase(4.4f, 1)]
        [TestCase(8.6f, 1)]
        [TestCase(9.4f, 2)]
        [TestCase(16.6f, 2)]
        [TestCase(17.4f, 3)]
        [TestCase(19.6f, 3)]
        public void OnlyScheduledPanelIsVisible(float elapsed, int expected)
        {
            var root = new GameObject("ResultTimingTest");
            root.SetActive(false); // 직렬화 참조를 채우기 전 Awake를 실행하지 않는다.
            try
            {
                var director = root.AddComponent<FlowDirector>();
                var screen = root.AddComponent<ResultScreen>();
                var flow = new DemoFlow(Load<FlowConfig>("FlowConfig"), Load<TankConfig>("TankConfig"), Load<AssistConfig>("AssistConfig"));
                typeof(FlowDirector).GetProperty("Flow").SetValue(director, flow);
                Set(screen, "director", director);
                Set(screen, "config", Load<ResultConfig>("ResultConfig"));
                Set(screen, "group", root.AddComponent<CanvasGroup>());
                var fields = new[] { "dateGroup", "nameGroup", "letterGroup", "titleGroup" };
                var groups = new CanvasGroup[fields.Length];
                for (int i = 0; i < groups.Length; i++)
                {
                    var child = new GameObject(fields[i]);
                    child.transform.SetParent(root.transform);
                    groups[i] = child.AddComponent<CanvasGroup>();
                    Set(screen, fields[i], groups[i]);
                }

                flow.DebugJumpTo(FlowSection.Result);
                flow.Tick(elapsed);
                typeof(ResultScreen).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(screen, null);

                for (int i = 0; i < groups.Length; i++)
                {
                    if (i == expected) Assert.Greater(groups[i].alpha, 0f, fields[i]);
                    else Assert.AreEqual(0f, groups[i].alpha, fields[i]);
                }
                if (elapsed > 19f)
                    Assert.Less(groups[3].alpha, 1f, "제목도 20초 안에 페이드아웃한다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static T Load<T>(string name) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>("Assets/_Project/Data/" + name + ".asset");

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
