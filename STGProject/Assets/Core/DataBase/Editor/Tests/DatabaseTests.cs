using System;
using System.Collections;
using System.Globalization;
using System.IO;
using GenjitsuLAB.Core;
using GenjitsuLAB.Data.Samples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GenjitsuLAB.Data.Editor.Tests
{
    /// <summary>Focused persistence, CSV, cross-table and runtime regression tests.</summary>
    public sealed class DatabaseTests
    {
        private string m_folder;
        private WeaponRepository m_weapons;
        private EnemyRepository m_enemies;

        [SetUp]
        public void SetUp()
        {
            m_folder = "Assets/DatabaseTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(m_folder));
            m_weapons = ScriptableObject.CreateInstance<WeaponRepository>();
            m_enemies = ScriptableObject.CreateInstance<EnemyRepository>();
            AssetDatabase.CreateAsset(m_weapons, m_folder + "/Weapons.asset");
            AssetDatabase.CreateAsset(m_enemies, m_folder + "/Enemies.asset");
            DatabaseEditorUtils.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            Database<WeaponData>.Shutdown();
            Database<EnemyData>.Shutdown();
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(m_folder);
            DatabaseEditorUtils.Invalidate();
        }

        [Test]
        public void Csv_QuotedNewlinesEscapesEmptyCellsAndBom()
        {
            CSVUtils.Table table = CSVUtils.ParseTable("\uFEFFkey,note,last\r\n001,\"hello, \"\"world\"\"\nnext\",\r\n002,,end");
            Assert.That(table.Rows.Count, Is.EqualTo(2));
            Assert.That(table.Rows[0][0], Is.EqualTo("001"));
            Assert.That(table.Rows[0][1], Is.EqualTo("hello, \"world\"\nnext"));
            Assert.That(table.Rows[0][2], Is.Empty);
            Assert.That(table.Lines[1], Is.EqualTo(4));
            Assert.Throws<FormatException>(() => CSVUtils.ParseTable("key,key\n1,2"));
            Assert.Throws<FormatException>(() => CSVUtils.ParseTable("key,note\n1,\"broken"));
            Assert.Throws<FormatException>(() => CSVUtils.ParseTable("key,note\n1"));
        }

        [Test]
        public void Csv_UsesInvariantCultureAndRoundTripsSupportedCells()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(DatabaseCSV.ParseCell(typeof(float), "0.125"), Is.EqualTo(0.125f));
                Assert.That(DatabaseCSV.ParseCell(typeof(Vector3), "1.5,2.5,3.5"), Is.EqualTo(new Vector3(1.5f, 2.5f, 3.5f)));
                Assert.That(((int[])DatabaseCSV.ParseCell(typeof(int[]), "")).Length, Is.Zero);
                Assert.Throws<FormatException>(() => DatabaseCSV.ParseCell(typeof(int), ""));
                Assert.Throws<FormatException>(() => DatabaseCSV.ParseCell(typeof(DayOfWeek), "99"));
                Assert.Throws<FormatException>(() => DatabaseCSV.ParseCell(typeof(Vector3), "1,2"));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void Batch_ForwardReferencesKeepRowsAndStableSubAssetIds()
        {
            string prefix = "batch_" + Guid.NewGuid().ToString("N");
            Import(prefix);
            WeaponData weapon = m_weapons.Records[0];
            EnemyData enemy = m_enemies.Records[0];
            Assert.That(AssetDatabase.IsSubAsset(weapon), Is.True);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(weapon, out string guid, out long id);
            using (DatabaseImport preview = DatabaseImport.Preview(new[]
            {
                new DatabaseImportSource(m_weapons, "key,m_damage\n" + prefix + ",9")
            }))
            {
                Assert.That(preview.Errors, Is.Empty);
                preview.Apply();
            }
            Assert.That(m_weapons.Records[0], Is.SameAs(weapon));
            Assert.That(weapon.Damage, Is.EqualTo(9));
            Assert.That(weapon.Speed, Is.EqualTo(0.25f));
            Assert.That(enemy.Weapon.GetKey(), Is.EqualTo(prefix));
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(weapon, out string updatedGuid, out long updatedId);
            Assert.That(updatedGuid, Is.EqualTo(guid));
            Assert.That(updatedId, Is.EqualTo(id));
            using (DatabaseImport preview = DatabaseImport.Preview(new[] { new DatabaseImportSource(m_weapons, "key\n") }))
            {
                Assert.That(preview.Changes[0], Does.StartWith("KEEP"));
                preview.Apply();
            }
            Assert.That(m_weapons.Count, Is.EqualTo(1));
            string csv = DatabaseCSV.ToCSV(m_weapons);
            using (DatabaseImport preview = DatabaseImport.Preview(new[] { new DatabaseImportSource(m_weapons, csv) }))
            {
                preview.Apply();
            }
            Assert.That(m_weapons.Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(m_weapons)).Length, Is.EqualTo(2));
        }

        [Test]
        public void InvalidOrCancelledImportsNeverMutateAssets()
        {
            string key = "invalid_" + Guid.NewGuid().ToString("N");
            string before = File.ReadAllText(m_folder + "/Weapons.asset");
            using (DatabaseImport invalid = DatabaseImport.Preview(new[] {
                new DatabaseImportSource(m_weapons, "key,m_damage\n" + key + ",bad\n" + key + ",2") }))
            {
                Assert.That(invalid.CanApply, Is.False);
                Assert.Throws<InvalidOperationException>(() => invalid.Apply());
            }
            using (DatabaseImport missing = DatabaseImport.Preview(new[] {
                new DatabaseImportSource(m_enemies, "key,m_weapon\n" + key + ",missing_" + key) }))
            {
                Assert.That(missing.CanApply, Is.False);
                Assert.That(string.Join("\n", missing.Errors), Does.Contain("line 2"));
            }
            using (DatabaseImport cancelled = DatabaseImport.Preview(new[] {
                new DatabaseImportSource(m_weapons, "key\n" + key) }))
            {
                Assert.That(cancelled.CanApply, Is.True);
            }
            Assert.That(m_weapons.Count, Is.Zero);
            Assert.That(File.ReadAllText(m_folder + "/Weapons.asset"), Is.EqualTo(before));
        }

        [Test]
        public void ReferencedRenameDeleteAndCrossAssetDuplicatesAreBlocked()
        {
            string key = "ref_" + Guid.NewGuid().ToString("N");
            Import(key);
            WeaponData record = m_weapons.Records[0];
            Assert.Throws<InvalidOperationException>(() => DatabaseEditorUtils.Delete(m_weapons, record));
            WeaponData draft = Object.Instantiate(record);
            try
            {
                draft.key += "_renamed";
                Assert.Throws<InvalidOperationException>(() => DatabaseEditorUtils.Save(m_weapons, draft, record));
            }
            finally
            {
                Object.DestroyImmediate(draft);
            }
            WeaponRepository other = ScriptableObject.CreateInstance<WeaponRepository>();
            AssetDatabase.CreateAsset(other, m_folder + "/Other.asset");
            DatabaseEditorUtils.Invalidate();
            using (DatabaseImport preview = DatabaseImport.Preview(new[] {
                new DatabaseImportSource(other, "key\n" + key) }))
            {
                Assert.That(preview.CanApply, Is.False);
            }
        }

        [Test]
        public void DeleteLastRecordRetainsAssetAndSupportsUndoRedo()
        {
            string key = "undo_" + Guid.NewGuid().ToString("N");
            using (DatabaseImport preview = DatabaseImport.Preview(new[] { new DatabaseImportSource(m_weapons, "key\n" + key) }))
            {
                preview.Apply();
            }
            Undo.PerformUndo();
            Assert.That(m_weapons.Count, Is.Zero);
            Undo.PerformRedo();
            Assert.That(m_weapons.Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.IsSubAsset(m_weapons.Records[0]), Is.True);
            DatabaseEditorUtils.Delete(m_weapons, m_weapons.Records[0]);
            Assert.That(m_weapons.Count, Is.Zero);
            Assert.That(File.Exists(m_folder + "/Weapons.asset"), Is.True);
            Undo.PerformUndo();
            Assert.That(m_weapons.Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.IsSubAsset(m_weapons.Records[0]), Is.True);
            Undo.PerformRedo();
            Assert.That(m_weapons.Count, Is.Zero);
        }

        [Test]
        public void StalePreviewCannotOverwriteNewerChanges()
        {
            string key = "stale_" + Guid.NewGuid().ToString("N");
            using (DatabaseImport stale = DatabaseImport.Preview(new[] { new DatabaseImportSource(m_weapons, "key\n" + key) }))
            {
                using (DatabaseImport newer = DatabaseImport.Preview(new[] { new DatabaseImportSource(m_weapons, "key\n" + key + "2") }))
                {
                    newer.Apply();
                }
                Assert.Throws<InvalidOperationException>(() => stale.Apply());
            }
        }

        [Test]
        public void RuntimeInitializationFailurePreservesIndexAndLookupAllocatesNothing()
        {
            WeaponData record = ScriptableObject.CreateInstance<WeaponData>();
            try
            {
                record.key = "runtime";
                Database<WeaponData>.Initialize(new[] { record });
                Assert.Throws<ArgumentException>(() => Database<WeaponData>.Initialize(new[] { record, record }));
                WeaponReference reference = new WeaponReference();
                reference.SetKey(record.key);
                for (int i = 0; i < 100; i++)
                {
                    reference.TryResolve(out _);
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                bool valid = true;
                for (int i = 0; i < 10000; i++)
                {
                    valid &= reference.TryResolve(out WeaponData found) && ReferenceEquals(found, record);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(valid, Is.True);
                Assert.That(allocated, Is.Zero);
                Database<WeaponData>.Shutdown();
                Assert.That(reference.TryResolve(out _), Is.False);
                Database<WeaponData>.Initialize(new[] { record });
                Assert.That(reference.Load(), Is.SameAs(record));
            }
            finally
            {
                Object.DestroyImmediate(record);
            }
        }

        [Test]
        public void DownloadsRejectHtmlHttpFailuresAndMalformedIdentifiers()
        {
            Assert.That(GoogleSheetDownloader.ValidateResponse(true, "text/csv", "key\n001"), Is.Null);
            Assert.That(GoogleSheetDownloader.ValidateResponse(true, "text/html", "<html>login</html>"), Is.Not.Null);
            Assert.That(GoogleSheetDownloader.ValidateResponse(false, "text/csv", "key\n001"), Is.Not.Null);
            Assert.That(GoogleSheetDownloader.ValidateResponse(true, "text/csv", "  "), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => GoogleSheetDownloader.GetCsvUrl("https://example.com", "0"));
            Assert.Throws<ArgumentException>(() => GoogleSheetDownloader.GetCsvUrl("sheet", "-1"));
        }

        private void Import(string key)
        {
            using (DatabaseImport preview = DatabaseImport.Preview(new[]
            {
                new DatabaseImportSource(m_enemies, "key,m_weapon\n" + key + "_enemy," + key),
                new DatabaseImportSource(m_weapons, "key,m_speed\n" + key + ",0.25")
            }))
            {
                Assert.That(preview.Errors, Is.Empty);
                preview.Apply();
            }
        }
    }
}
