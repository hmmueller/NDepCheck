﻿using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Matching;
using NDepCheck.Reading.AssemblyReading;
using NDepCheck.Transforming.Projecting;
using NDepCheck.Transforming.ViolationChecking;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class TestPatternsAndItems {
        private const bool IGNORECASE = false;

        [TestMethod]
        public void TestSimpleDependencyRuleMatches() {
            ItemType itemType = ItemType.New("NC", new[] { "Namespace", "Class" }, new[] { "", "" }, ignoreCase: false);

            var r1 = CreateDependencyRule(itemType, ":", ":");

            var rn1 = CreateDependencyRule(itemType, "n*", ":");
            var rn2 = CreateDependencyRule(itemType, "n*:", ":");
            var rn3 = CreateDependencyRule(itemType, "n**", ":");
            var rn4 = CreateDependencyRule(itemType, "n**:", ":");
            var rn5 = CreateDependencyRule(itemType, "n1", ":");
            var rn6 = CreateDependencyRule(itemType, "n1:", ":");
            var rn7 = CreateDependencyRule(itemType, "*n1:", ":");
            var rn8 = CreateDependencyRule(itemType, "**n1:", ":");
            var rc1 = CreateDependencyRule(itemType, ":c*", ":");
            var rc2 = CreateDependencyRule(itemType, ":c**", ":");
            var rc3 = CreateDependencyRule(itemType, ":*c1", ":");
            var rc4 = CreateDependencyRule(itemType, ":**c1", ":");
            var rnc1 = CreateDependencyRule(itemType, "n*:c*", ":");
            var rnc2 = CreateDependencyRule(itemType, "n**:c**", ":");

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Dependency dep = graph.CreateDependency(graph.CreateItem(itemType, "n1", "c1"), graph.CreateItem(itemType, "n2", "c2"), null, "Test", ct: 1);
            Assert.IsTrue(r1.IsMatch(dep));

            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsTrue(rn4.IsMatch(dep));
            Assert.IsTrue(rn5.IsMatch(dep));
            Assert.IsTrue(rn6.IsMatch(dep));
            Assert.IsTrue(rn7.IsMatch(dep));
            Assert.IsTrue(rn8.IsMatch(dep));

            Assert.IsTrue(rc1.IsMatch(dep));
            Assert.IsTrue(rc2.IsMatch(dep));
            Assert.IsTrue(rc3.IsMatch(dep));
            Assert.IsTrue(rc4.IsMatch(dep));

            Assert.IsTrue(rnc1.IsMatch(dep));
            Assert.IsTrue(rnc2.IsMatch(dep));
        }

        private static DependencyRule CreateDependencyRule(ItemType itemType, string left, string right) {
            return new DependencyRule(new DependencyMatch(itemType, left, "", itemType, right, IGNORECASE), 
                new DependencyRuleSource("TEST", 0, left + "--->" + right, false, left)
            );
        }

        [TestMethod]
        public void TestBackReferenceDependencyRuleMatches() {
            ItemType itemtype = ItemType.New("SO", new[] { "Schema", "Object" }, new[] { "", "" }, ignoreCase: false);
            var rn1 = CreateDependencyRule(itemtype, "(s)*", @"\1*");
            var rn2 = CreateDependencyRule(itemtype, "(s)*:(t)*", @"\1*:\2*");
            var rn3 = CreateDependencyRule(itemtype, "(s)**:(t)**", @"\1*:\2*");
            var rn4 = CreateDependencyRule(itemtype, "s(*):t(*)", @"s\1:t\2");
            var rn5 = CreateDependencyRule(itemtype, "s(**):t(**)", @"s\1:t\2");

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Dependency dep = graph.CreateDependency(graph.CreateItem(itemtype, "s1", "t1"), graph.CreateItem(itemtype, "s2", "t2"), null, "Test", ct: 1);
            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsFalse(rn4.IsMatch(dep));
            Assert.IsFalse(rn5.IsMatch(dep));
        }

        [TestMethod]
        public void TestBackReferenceDependencyRuleMatchesWithOuterParentheses() {
            ItemType itemtype = ItemType.New("SO", new[] { "Schema", "Object" }, new[] { "", "" }, ignoreCase: false);
            var rn3 = CreateDependencyRule(itemtype, "(s**):(t**)", @"\1*:\2*");
            var rn5 = CreateDependencyRule(itemtype, "(**s**):(**t**)", @"\1:\2");

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Dependency dep = graph.CreateDependency(graph.CreateItem(itemtype, "sx", "tx"), graph.CreateItem(itemtype, "sx", "tx"), null, "Test", ct: 1);
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsTrue(rn5.IsMatch(dep));
        }

        [TestMethod]
        public void TestMoreBackReferenceDependencyRuleMatches() {
            ItemType itemType = ItemType.New("SO", new[] { "Schema", "Object" }, new[] { "", "" }, ignoreCase: false);
            var rn1 = CreateDependencyRule(itemType, "(s)*", @"\1*");
            var rn2 = CreateDependencyRule(itemType, "(s*)", @"\1");
            var rn3 = CreateDependencyRule(itemType, "s(*)", @"s\1");
            var rn4 = CreateDependencyRule(itemType, "s(*):(t)*", @"s\1:\2*");
            var rn5 = CreateDependencyRule(itemType, "s*:t(*)", @"s\1:t*");

            var rn6 = CreateDependencyRule(itemType, "s*:(t*)", @"s\1:t*");

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Dependency dep = graph.CreateDependency(graph.CreateItem(itemType, "s1", "t1"), graph.CreateItem(itemType, "s1", "t2"), null, "Test", ct: 1);
            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsTrue(rn4.IsMatch(dep));
            Assert.IsTrue(rn5.IsMatch(dep));
            Assert.IsFalse(rn6.IsMatch(dep));
        }

        [TestMethod]
        public void TestSimpleGraphAbstractionMatches() {
            ItemType testType = ItemType.New("NC", new[] { "Namespace", "Class" }, new[] { "", "" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "Name", }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "**():", null, IGNORECASE, true, true);

            var gn1 = new Projection(testType, simpleType, "(n)*", null, IGNORECASE, true, true);
            var gn2 = new Projection(testType, simpleType, "(n)*:", null, IGNORECASE, true, true);
            var gn3 = new Projection(testType, simpleType, "(n)**", null, IGNORECASE, true, true);
            var gn4 = new Projection(testType, simpleType, "(n)**:", null, IGNORECASE, true, true);
            var gn5 = new Projection(testType, simpleType, "(n)1", null, IGNORECASE, true, true);
            var gn6 = new Projection(testType, simpleType, "(n)1:", null, IGNORECASE, true, true);
            var gn7 = new Projection(testType, simpleType, "(*n)1:", null, IGNORECASE, true, true);
            var gn8 = new Projection(testType, simpleType, "**(n)1:", null, IGNORECASE, true, true);

            var hn1 = new Projection(testType, simpleType, "n(*)", null, IGNORECASE, true, true);
            var hn2 = new Projection(testType, simpleType, "n(*):", null, IGNORECASE, true, true);
            var hn3 = new Projection(testType, simpleType, "n(**)", null, IGNORECASE, true, true);
            var hn4 = new Projection(testType, simpleType, "n(**):", null, IGNORECASE, true, true);
            var hn5 = new Projection(testType, simpleType, "n(1)", null, IGNORECASE, true, true);
            var hn6 = new Projection(testType, simpleType, "n(1):", null, IGNORECASE, true, true);
            var hn7 = new Projection(testType, simpleType, "*n(1):", null, IGNORECASE, true, true);
            var hn8 = new Projection(testType, simpleType, "**n(1):", null, IGNORECASE, true, true);

            var gc1 = new Projection(testType, simpleType, ":(c)*", null, IGNORECASE, true, true);
            var gc2 = new Projection(testType, simpleType, ":(c)**", null, IGNORECASE, true, true);
            var gc3 = new Projection(testType, simpleType, ":(*c)1", null, IGNORECASE, true, true);
            var gc4 = new Projection(testType, simpleType, ":(**c)1", null, IGNORECASE, true, true);

            var hc1 = new Projection(testType, simpleType, ":c(*)", null, IGNORECASE, true, true);
            var hc2 = new Projection(testType, simpleType, ":c(*)*", null, IGNORECASE, true, true);
            var hc3 = new Projection(testType, simpleType, ":*c(1)", null, IGNORECASE, true, true);
            var hc4 = new Projection(testType, simpleType, ":**c(1)", null, IGNORECASE, true, true);

            var gnc1 = new Projection(testType, simpleType, "n(*):(c)*", new[] { "\\1+\\2" }, IGNORECASE, true, true);
            var gnc2 = new Projection(testType, simpleType, "n(**):(c)**", new[] { "\\1+\\2" }, IGNORECASE, true, true);

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item i = graph.CreateItem(testType, "n1", "c1");

            Assert.AreEqual("", g1.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("n", gn1.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn2.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn3.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn4.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn5.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn6.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn7.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn8.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("1", hn1.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn2.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn3.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn4.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn5.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn6.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn7.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn8.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("c", gc1.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc2.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc3.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc4.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("1", hc1.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hc2.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hc3.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hc4.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("1+c", gnc1.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1+c", gnc2.Match(gc.CurrentGraph, i, true).Name);
        }

        [TestMethod]
        public void TestRegexGraphAbstractionMatches() {
            ItemType testType = ItemType.New("NC", new[] { "Namespace", "Class" }, new[] { "", "" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "Name" }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "^.*()$:", null, IGNORECASE, true, true);

            // ReSharper disable InconsistentNaming
            var gn1a = new Projection(testType, simpleType, "^(n).*", null, IGNORECASE, true, true);
            var gn1b = new Projection(testType, simpleType, "^(n).*$", null, IGNORECASE, true, true);
            var gn1c = new Projection(testType, simpleType, "(n).*$", null, IGNORECASE, true, true);
            var gn2a = new Projection(testType, simpleType, "^(n).*:", null, IGNORECASE, true, true);
            var gn2b = new Projection(testType, simpleType, "^(n).*$:", null, IGNORECASE, true, true);
            var gn2c = new Projection(testType, simpleType, "(n).*$:", null, IGNORECASE, true, true);
            var gn5a = new Projection(testType, simpleType, "^(n)1", null, IGNORECASE, true, true);
            var gn5b = new Projection(testType, simpleType, "^(n)1$", null, IGNORECASE, true, true);
            var gn5c = new Projection(testType, simpleType, "(n)1$", null, IGNORECASE, true, true);
            var gn6a = new Projection(testType, simpleType, "^(n)1:", null, IGNORECASE, true, true);
            var gn6b = new Projection(testType, simpleType, "^(n)1$:", null, IGNORECASE, true, true);
            var gn6c = new Projection(testType, simpleType, "(n)1$:", null, IGNORECASE, true, true);
            var gn7a = new Projection(testType, simpleType, "^(.*n)1:", null, IGNORECASE, true, true);
            var gn7b = new Projection(testType, simpleType, "^(.*n)1$:", null, IGNORECASE, true, true);
            var gn7c = new Projection(testType, simpleType, "(.*n)1$:", null, IGNORECASE, true, true);

            var hn1a = new Projection(testType, simpleType, "^n(.*)", null, IGNORECASE, true, true);
            var hn1b = new Projection(testType, simpleType, "^n(.*)$", null, IGNORECASE, true, true);
            var hn1c = new Projection(testType, simpleType, "n(.*)$", null, IGNORECASE, true, true);
            var hn2a = new Projection(testType, simpleType, "^n(.*):", null, IGNORECASE, true, true);
            var hn2b = new Projection(testType, simpleType, "^n(.*)$:", null, IGNORECASE, true, true);
            var hn2c = new Projection(testType, simpleType, "n(.*)$:", null, IGNORECASE, true, true);
            var hn5a = new Projection(testType, simpleType, "^n(1)", null, IGNORECASE, true, true);
            var hn5b = new Projection(testType, simpleType, "^n(1)$", null, IGNORECASE, true, true);
            var hn5c = new Projection(testType, simpleType, "n(1)$", null, IGNORECASE, true, true);
            var hn6a = new Projection(testType, simpleType, "^n(1):", null, IGNORECASE, true, true);
            var hn6b = new Projection(testType, simpleType, "^n(1)$:", null, IGNORECASE, true, true);
            var hn6c = new Projection(testType, simpleType, "n(1)$:", null, IGNORECASE, true, true);
            var hn7a = new Projection(testType, simpleType, "^.*n(1):", null, IGNORECASE, true, true);
            var hn7b = new Projection(testType, simpleType, "^.*n(1)$:", null, IGNORECASE, true, true);
            var hn7c = new Projection(testType, simpleType, ".*n(1)$:", null, IGNORECASE, true, true);

            var gc1a = new Projection(testType, simpleType, ":^(c).*", null, IGNORECASE, true, true);
            var gc1b = new Projection(testType, simpleType, ":^(c).*$", null, IGNORECASE, true, true);
            var gc1c = new Projection(testType, simpleType, ":(c).*$", null, IGNORECASE, true, true);
            var gc3a = new Projection(testType, simpleType, ":^(.*c)1", null, IGNORECASE, true, true);
            var gc3b = new Projection(testType, simpleType, ":^(.*c)1$", null, IGNORECASE, true, true);
            var gc3c = new Projection(testType, simpleType, ":(.*c)1$", null, IGNORECASE, true, true);
            // ReSharper restore InconsistentNaming

            //var hc1 = new GraphAbstraction_(":c(*)", false);
            //var hc2 = new GraphAbstraction_(":c(*)*", false);
            //var hc3 = new GraphAbstraction_(":*c(1)", false);
            //var hc4 = new GraphAbstraction_(":**c(1)", false);

            //var gnc1 = new GraphAbstraction_("n(*):(c)*", false);
            //var gnc2 = new GraphAbstraction_("n(**):(c)**", false);

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item i = graph.CreateItem(testType, "n1", "c1");

            Assert.AreEqual("", g1.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("n", gn1a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn1b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn1c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn2a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn2b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn2c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn5a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn5b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn5c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn6a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn6b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn6c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn7a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn7b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("n", gn7c.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("1", hn1a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn1b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn1c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn2a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn2b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn2c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn5a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn5b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn5c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn6a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn6b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn6c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn7a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn7b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("1", hn7c.Match(gc.CurrentGraph, i, true).Name);

            Assert.AreEqual("c", gc1a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc1b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc1c.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc3a.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc3b.Match(gc.CurrentGraph, i, true).Name);
            Assert.AreEqual("c", gc3c.Match(gc.CurrentGraph, i, true).Name);

            //Assert.AreEqual("1", hc1.Match(i, true).Name);
            //Assert.AreEqual("1", hc2.Match(i, true).Name);
            //Assert.AreEqual("1", hc3.Match(i, true).Name);
            //Assert.AreEqual("1", hc4.Match(i, true).Name);

            //Assert.AreEqual("1c", gnc1.Match(i, true).Name);
            //Assert.AreEqual("1c", gnc2.Match(i, true).Name);
        }

        [TestMethod]
        public void TestAsterisks() {
            ItemType testType = ItemType.New("T", new[] { "Name", }, new[] { "" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "Name", }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "(**)", null, IGNORECASE, true, true);

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Assert.AreEqual("n1", g1.Match(gc.CurrentGraph, graph.CreateItem(testType, "n1"), true).Name);
            Assert.AreEqual("n1.n2", g1.Match(gc.CurrentGraph, graph.CreateItem(testType, "n1.n2"), true).Name);
            Assert.AreEqual("n1.n2.n3", g1.Match(gc.CurrentGraph, graph.CreateItem(testType, "n1.n2.n3"), true).Name);
        }
        //

        [TestMethod]
        public void TestProblemWithTests() {
            ItemType testType = ItemType.New("A", new[] { "Assembly", }, new[] { "Name" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "Name", }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "**Tests**", null, IGNORECASE, true, true);
            var g2 = new Projection(testType, simpleType, "**Tests**()", null, IGNORECASE, true, true);

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Assert.AreEqual("\\1", g1.Match(gc.CurrentGraph, graph.CreateItem(testType, "Framework.Core.NunitTests.IBOL"), true).Name);
            Assert.AreEqual("", g2.Match(gc.CurrentGraph, graph.CreateItem(testType, "Framework.Core.NunitTests.IBOL"), true).Name);
        }

        [TestMethod]
        public void TestProblemWithEmptyNamespace() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETITEM;

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            var @using = graph.CreateItem(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "");
            var used = graph.CreateItem(itemType, "System", "Object", "mscorlib", "", "", "");
            var d = graph.CreateDependency(@using, used, null, "Test", ct: 1);

            var r = CreateDependencyRule(itemType, "**", "System.**");
            Assert.IsTrue(r.IsMatch(d));
        }

        [TestMethod]
        public void TestEmptyNamespaceInRule() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETITEM;

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item @using = graph.CreateItem(itemType, "NDepCheck.TestAssembly.dir1.dir2", "SomeClass", "NDepCheck.TestAssembly", "1.0.0.0", "", "AnotherMethod");
            Item used = graph.CreateItem(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "I");
            var d = graph.CreateDependency(@using, used, null, "Test", ct: 1);

            var r = CreateDependencyRule(itemType, "NDepCheck.TestAssembly.dir1.dir2:SomeClass:**", "-:NamespacelessTestClassForNDepCheck::I");
            Assert.IsTrue(r.IsMatch(d));
        }


        [TestMethod]
        public void TestAmpersand() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETITEM;

            var ga = new Projection(itemType, itemType, "(**):(**):(**)", new[] { "\\1", "\\2", "\\3", "", "", "" },
                                    ignoreCase: false, forLeftSide: true, forRightSide: true);

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item used = graph.CreateItem(itemType, "System", "Byte&", "mscorlib", "4.0.0.0", "", "");

            string result = ga.Match(gc.CurrentGraph, used, true).Name;

            Assert.AreEqual("System:Byte&:mscorlib;;:", result);
        }

        [TestMethod]
        public void TestSimpleNamedCall() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETITEM;
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            Item @using = graph.CreateItem(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "");
            Item used = graph.CreateItem(itemType, "System", "Object", "mscorlib", "", "", "");
            var d = graph.CreateDependency(@using, used, null, "Test", ct: 1);

            DependencyRule r = CreateDependencyRule(itemType, "**", "Assembly.Name=mscorlib");
            Assert.IsTrue(r.IsMatch(d));
        }

        [TestMethod]
        public void TestReverseFieldsInNamedCall() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETITEM;
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item @using = graph.CreateItem(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "");
            Item used = graph.CreateItem(itemType, "System", "Object", "mscorlib", "", "", "");
            var d = graph.CreateDependency(@using, used, null, "Test", ct: 1);

            DependencyRule r = CreateDependencyRule(itemType, "**", "Assembly.Name=mscorlib:Class=Object");
            Assert.IsTrue(r.IsMatch(d));
        }

        [TestMethod]
        public void TestItemDependencyItemMatch() {
            DependencyMatch.Create("a--'b->c", false);
            DependencyMatch.Create("!->c", false);
            DependencyMatch.Create("a--#'b", false);
            DependencyMatch.Create("'b", false);

            DependencyMatch.Create(" a -- ?'b -> c ", false);
            DependencyMatch.Create("       'b -> c ", false);
            DependencyMatch.Create(" a --  'b      ", false);
            DependencyMatch.Create("       'b      ", false);
        }
    }
}
