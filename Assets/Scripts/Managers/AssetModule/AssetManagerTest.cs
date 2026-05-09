using System.Collections;
using UnityEngine;

namespace GameDemo
{
    /// <summary>
    /// Unit tests for AssetManager. Attach to a GameObject in the sample scene.
    /// Right-click the component header in the Inspector to run individual tests.
    /// Tests with "ASYNC" in the label require Play Mode.
    /// </summary>
    public class AssetManagerTest : MonoBehaviour
    {
        const string TestPath = "Test/test_data";
        const string TestPathJson = "Test/test_config";
        const string InvalidPath = "Test/nonexistent";

        int _passCount;
        int _failCount;

        void AssertTrue(bool condition, string testName)
        {
            if (condition)
            {
                _passCount++;
                Debug.Log($"<color=green>[PASS]</color> {testName}");
            }
            else
            {
                _failCount++;
                Debug.LogError($"<color=red>[FAIL]</color> {testName}");
            }
        }

        void BeginSuite(string suiteName)
        {
            _passCount = 0;
            _failCount = 0;
            Debug.Log($"\n<color=cyan>=== {suiteName} ===</color>");
        }

        void EndSuite()
        {
            Debug.Log($"<color=cyan>Done: {_passCount} passed, {_failCount} failed</color>");
        }

        #region Synchronous Tests (Edit Mode safe)

        [ContextMenu("1. Sync Load Success")]
        public void TestLoadSuccess()
        {
            BeginSuite("Sync Load Success");
            AssetManager.Instance.ClearCache();

            var asset = AssetManager.Instance.Load<TextAsset>(TestPath);
            AssertTrue(asset != null, "Loaded asset is not null");
            AssertTrue(asset.text.Contains("AssetManager test resource"), "Asset content is correct");

            EndSuite();
        }

        [ContextMenu("2. Sync Load Fail")]
        public void TestLoadFail()
        {
            BeginSuite("Sync Load Fail");
            AssetManager.Instance.ClearCache();

            var asset = AssetManager.Instance.Load<TextAsset>(InvalidPath);
            AssertTrue(asset == null, "Loading invalid path returns null");

            EndSuite();
        }

        [ContextMenu("3. Sync Load Cached (Ref Count)")]
        public void TestLoadCached()
        {
            BeginSuite("Sync Load Cached");
            AssetManager.Instance.ClearCache();

            var a1 = AssetManager.Instance.Load<TextAsset>(TestPath);
            AssertTrue(a1 != null, "First load succeeds");
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 1, "Ref count = 1 after first load");

            var a2 = AssetManager.Instance.Load<TextAsset>(TestPath);
            AssertTrue(a2 != null, "Second load succeeds");
            AssertTrue(ReferenceEquals(a1, a2), "Second load returns same cached instance");
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 2, "Ref count = 2 after second load");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("4. Query Methods")]
        public void TestQuery()
        {
            BeginSuite("Query Methods");
            AssetManager.Instance.ClearCache();

            AssertTrue(!AssetManager.Instance.IsLoaded(TestPath), "IsLoaded = false before load");
            AssertTrue(!AssetManager.Instance.IsLoading(TestPath), "IsLoading = false before load");
            AssertTrue(AssetManager.Instance.Get<TextAsset>(TestPath) == null, "Get returns null before load");
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 0, "Ref count = 0 before load");
            AssertTrue(AssetManager.Instance.CacheCount == 0, "CacheCount = 0 before load");

            AssetManager.Instance.Load<TextAsset>(TestPath);

            AssertTrue(AssetManager.Instance.IsLoaded(TestPath), "IsLoaded = true after load");
            AssertTrue(AssetManager.Instance.Get<TextAsset>(TestPath) != null, "Get returns asset after load");
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 1, "Ref count = 1 after load");
            AssertTrue(AssetManager.Instance.CacheCount == 1, "CacheCount = 1 after load");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("5. Unload By Path")]
        public void TestUnloadByPath()
        {
            BeginSuite("Unload By Path");
            AssetManager.Instance.ClearCache();

            AssetManager.Instance.Load<TextAsset>(TestPath);
            AssertTrue(AssetManager.Instance.IsLoaded(TestPath), "Asset loaded before unload");

            var unloaded = AssetManager.Instance.Unload(TestPath);
            AssertTrue(unloaded, "Unload returns true (ref count reached 0)");
            AssertTrue(!AssetManager.Instance.IsLoaded(TestPath), "Asset not in cache after unload");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("6. Unload With Ref Count")]
        public void TestUnloadRefCount()
        {
            BeginSuite("Unload With Ref Count");
            AssetManager.Instance.ClearCache();

            AssetManager.Instance.Load<TextAsset>(TestPath);
            AssetManager.Instance.Load<TextAsset>(TestPath);
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 2, "Ref count = 2 after two loads");

            var result1 = AssetManager.Instance.Unload(TestPath);
            AssertTrue(!result1, "Unload returns false (ref count > 0)");
            AssertTrue(AssetManager.Instance.IsLoaded(TestPath), "Asset still cached (ref = 1)");
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 1, "Ref count = 1 after one unload");

            var result2 = AssetManager.Instance.Unload(TestPath);
            AssertTrue(result2, "Unload returns true (ref count = 0)");
            AssertTrue(!AssetManager.Instance.IsLoaded(TestPath), "Asset removed from cache");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("7. Force Unload")]
        public void TestForceUnload()
        {
            BeginSuite("Force Unload");
            AssetManager.Instance.ClearCache();

            AssetManager.Instance.Load<TextAsset>(TestPath);
            AssetManager.Instance.Load<TextAsset>(TestPath);
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 2, "Ref count = 2 after two loads");

            AssetManager.Instance.ForceUnload(TestPath);
            AssertTrue(!AssetManager.Instance.IsLoaded(TestPath), "Asset removed despite ref count > 0");
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 0, "Ref count cleared after force unload");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("8. Clear Cache")]
        public void TestClearCache()
        {
            BeginSuite("Clear Cache");
            AssetManager.Instance.ClearCache();

            AssetManager.Instance.Load<TextAsset>(TestPath);
            AssetManager.Instance.Load<TextAsset>(TestPathJson);
            AssertTrue(AssetManager.Instance.CacheCount == 2, "CacheCount = 2 after two loads");

            AssetManager.Instance.ClearCache();
            AssertTrue(AssetManager.Instance.CacheCount == 0, "CacheCount = 0 after ClearCache");
            AssertTrue(!AssetManager.Instance.IsLoaded(TestPath), "Asset 1 removed");
            AssertTrue(!AssetManager.Instance.IsLoaded(TestPathJson), "Asset 2 removed");

            EndSuite();
        }

        [ContextMenu("9. Load Different Types")]
        public void TestLoadDifferentTypes()
        {
            BeginSuite("Load Different Types");
            AssetManager.Instance.ClearCache();

            var txt = AssetManager.Instance.Load<TextAsset>(TestPath);
            AssertTrue(txt != null, "Loaded as TextAsset");

            AssetManager.Instance.ClearCache();
            var tex = AssetManager.Instance.Load<Texture2D>(TestPath);
            AssertTrue(tex == null, "Loading text file as Texture2D returns null");

            EndSuite();
        }

        #endregion

        #region Asynchronous Tests (Play Mode required)

        [ContextMenu("10. ASYNC Load - Handle")]
        public void TestAsyncLoadHandle()
        {
            StartCoroutine(TestAsyncLoadHandleCoroutine());
        }

        IEnumerator TestAsyncLoadHandleCoroutine()
        {
            BeginSuite("Async Load - Handle");
            AssetManager.Instance.ClearCache();

            var handle = AssetManager.Instance.LoadAsync<TextAsset>(TestPath);
            AssertTrue(handle != null, "Handle returned immediately");

            if (!handle.IsDone)
            {
                AssertTrue(AssetManager.Instance.IsLoading(TestPath), "IsLoading = true during async load");
                yield return new WaitUntil(() => handle.IsDone);
            }

            AssertTrue(handle.IsDone, "Handle is done after loading");
            AssertTrue(handle.Progress >= 1f, "Progress reached 1.0");
            AssertTrue(handle.GetResult<TextAsset>() != null, "GetResult returns asset");
            AssertTrue(AssetManager.Instance.IsLoaded(TestPath), "Asset cached after async load");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("11. ASYNC Load - Cached Returns Immediate")]
        public void TestAsyncLoadCached()
        {
            StartCoroutine(TestAsyncLoadCachedCoroutine());
        }

        IEnumerator TestAsyncLoadCachedCoroutine()
        {
            BeginSuite("Async Load - Cached Returns Immediate");
            AssetManager.Instance.ClearCache();

            AssetManager.Instance.Load<TextAsset>(TestPath);

            var handle = AssetManager.Instance.LoadAsync<TextAsset>(TestPath);
            AssertTrue(handle != null, "Handle returned");
            AssertTrue(handle.IsDone, "Handle is already done (cached)");
            AssertTrue(handle.GetResult<TextAsset>() != null, "GetResult returns cached asset");
            AssertTrue(AssetManager.Instance.GetRefCount(TestPath) == 2, "Ref count = 2 (sync + async both counted)");

            yield return null;

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("12. ASYNC Load - Unload By Handle")]
        public void TestAsyncLoadUnloadByHandle()
        {
            StartCoroutine(TestAsyncLoadUnloadByHandleCoroutine());
        }

        IEnumerator TestAsyncLoadUnloadByHandleCoroutine()
        {
            BeginSuite("Async Load - Unload By Handle");
            AssetManager.Instance.ClearCache();

            var handle = AssetManager.Instance.LoadAsync<TextAsset>(TestPath);
            yield return new WaitUntil(() => handle.IsDone);

            AssertTrue(AssetManager.Instance.IsLoaded(TestPath), "Asset cached after async load");

            var unloaded = AssetManager.Instance.Unload(handle);
            AssertTrue(unloaded, "Unload(handle) returns true");
            AssertTrue(!AssetManager.Instance.IsLoaded(TestPath), "Asset removed from cache");

            AssertTrue(!AssetManager.Instance.Unload((AssetHandle)null), "Unload(null) returns false");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        [ContextMenu("13. ASYNC Load - OnCompleted Callback")]
        public void TestAsyncLoadOnCompleted()
        {
            StartCoroutine(TestAsyncLoadOnCompletedCoroutine());
        }

        IEnumerator TestAsyncLoadOnCompletedCoroutine()
        {
            BeginSuite("Async Load - OnCompleted Callback");
            AssetManager.Instance.ClearCache();

            var callbackFired = false;
            TextAsset result = null;

            var handle = AssetManager.Instance.LoadAsync<TextAsset>(TestPath);
            handle.OnCompleted += () =>
            {
                callbackFired = true;
                result = handle.GetResult<TextAsset>();
            };

            yield return new WaitUntil(() => handle.IsDone);

            AssertTrue(callbackFired, "OnCompleted callback fired");
            AssertTrue(result != null, "Callback received valid asset");

            var lateCallbackFired = false;
            handle.OnCompleted += () => { lateCallbackFired = true; };
            AssertTrue(lateCallbackFired, "Late OnCompleted fires immediately");

            AssetManager.Instance.ClearCache();
            EndSuite();
        }

        #endregion

        #region Run All

        [ContextMenu("Run All Sync Tests")]
        public void RunAllSyncTests()
        {
            Debug.Log("\n<color=yellow>========== RUNNING ALL SYNC TESTS ==========</color>");
            TestLoadSuccess();
            TestLoadFail();
            TestLoadCached();
            TestQuery();
            TestUnloadByPath();
            TestUnloadRefCount();
            TestForceUnload();
            TestClearCache();
            TestLoadDifferentTypes();
            Debug.Log("<color=yellow>========== ALL SYNC TESTS COMPLETE ==========</color>\n");
        }

        [ContextMenu("Run All Async Tests")]
        public void RunAllAsyncTests()
        {
            StartCoroutine(RunAllAsyncTestsCoroutine());
        }

        IEnumerator RunAllAsyncTestsCoroutine()
        {
            Debug.Log("\n<color=yellow>========== RUNNING ALL ASYNC TESTS ==========</color>");

            yield return TestAsyncLoadHandleCoroutine();
            yield return TestAsyncLoadCachedCoroutine();
            yield return TestAsyncLoadUnloadByHandleCoroutine();
            yield return TestAsyncLoadOnCompletedCoroutine();

            Debug.Log("<color=yellow>========== ALL ASYNC TESTS COMPLETE ==========</color>\n");
        }

        #endregion
    }
}
