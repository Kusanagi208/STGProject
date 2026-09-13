using System;
using System.Collections;
using GenjitsuLAB.Data.Samples;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GenjitsuLAB.Data.Editor.Tests
{
    /// <summary>Exercises catalog lifetime and key resolution in real Play Mode.</summary>
    public sealed class DatabasePlayModeTests
    {
        /// <summary>Initializes, queries and releases the sample catalog repeatedly without per-query GC.</summary>
        [UnityTest]
        public IEnumerator SampleCatalog_ResolvesAndRestartsWithoutAllocations()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            DatabaseCatalog catalog = AssetDatabase.LoadAssetAtPath<DatabaseCatalog>("Assets/Core/DataBase/Samples/Catalog.asset");
            Assert.That(catalog, Is.Not.Null);
            catalog.Initialize();
            Assert.That(Database<EnemyData>.TryGet("enemy_001", out EnemyData enemy), Is.True);
            Assert.That(enemy.Weapon.TryResolve(out WeaponData weapon), Is.True);
            Assert.That(weapon.Damage, Is.EqualTo(1));
            for (int i = 0; i < 100; i++)
            {
                enemy.Weapon.TryResolve(out _);
            }
            long start = GC.GetAllocatedBytesForCurrentThread();
            bool allResolved = true;
            for (int i = 0; i < 10000; i++)
            {
                allResolved &= enemy.Weapon.TryResolve(out WeaponData found) && ReferenceEquals(found, weapon);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.That(allResolved, Is.True);
            Assert.That(bytes, Is.Zero);
            catalog.Initialize();
            Assert.That(Database<WeaponData>.Load("weapon_001"), Is.SameAs(weapon));
            catalog.Shutdown();
            Assert.That(Database<WeaponData>.Exists("weapon_001"), Is.False);
            catalog.Initialize();
            Assert.That(Database<WeaponData>.Exists("weapon_001"), Is.True);
            catalog.Shutdown();
            yield return new ExitPlayMode();
        }
    }
}
