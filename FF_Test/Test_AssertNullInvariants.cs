using System;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using NUnit.Framework.Internal;
using snns;

namespace Test_AssertNullInvariants
{
public class RecursionAndEnumeration
{
	public enum Behavior
	{
		Throws,
		DoesNotThrow
	}

	[Test]
	// empty collections never over-enumerate
	[TestCase(0, 0, Behavior.DoesNotThrow)]
	[TestCase(0, 1, Behavior.DoesNotThrow)]
	[TestCase(0, long.MaxValue, Behavior.DoesNotThrow)]
	// fencepost error near 1 item
	[TestCase(1, 0, Behavior.Throws)]
	[TestCase(1, 1, Behavior.DoesNotThrow)]
	[TestCase(1, 2, Behavior.DoesNotThrow)]
	[TestCase(1, long.MaxValue, Behavior.DoesNotThrow)]
	// general case
	[TestCase(100, 0, Behavior.Throws)]
	[TestCase(100, 1, Behavior.Throws)]
	[TestCase(100, 99, Behavior.Throws)]
	[TestCase(100, 100, Behavior.DoesNotThrow)]
	[TestCase(100, 101, Behavior.DoesNotThrow)]
	[TestCase(100, long.MaxValue, Behavior.DoesNotThrow)]
	public void ThrowsIfCollectionHasMoreItemsThanEnumerationLimit(int items, long enumerationLimit, Behavior behavior)
	{
		var collection = Enumerable.Range(0, items);

		switch (behavior)
		{
			case Behavior.Throws:
				Assert.Throws<InvariantException>(
					() => FF.AssertNullableInvariants(collection, enumerationLimit: enumerationLimit));
				break;
			case Behavior.DoesNotThrow:
				Assert.DoesNotThrow(
					() => FF.AssertNullableInvariants(collection, enumerationLimit: enumerationLimit));
				break;
			default:
				Assert.Fail();
				break;
		}
	}

	public class Wrapper
	{
		public Wrapper? W { get; set; }
	}

	[Test]
	public void ThrowsIfObjectHasMoreLinksThanTheRecursionLimit()
	{
		Wrapper w = new Wrapper();

		for (var i = 0; i < 5; i++)
		{
			var wrap = new Wrapper();
			wrap.W = w;
			w = wrap;
		}

		Assert.NotNull( /**/ w.W);
		Assert.NotNull( /**/ w.W!.W);
		Assert.NotNull( /**/ w.W!.W!.W);
		Assert.NotNull( /**/ w.W!.W!.W!.W);
		Assert.NotNull( /**/ w.W!.W!.W!.W!.W);
		Assert.Null( /*****/ w.W!.W!.W!.W!.W!.W);

		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(w));

		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(w, recursionLimit: 1));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(w, recursionLimit: 2));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(w, recursionLimit: 3));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(w, recursionLimit: 4));
		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(w, recursionLimit: 5));
		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(w, recursionLimit: 6));

		// create a cyclic graph
		w.W = w;

		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(w));
	}

	public class Nightmare
	{
		public List<Nightmare>? List { get; set; }
		public string String { get; set; } = "required";
	}

	public Nightmare CreateNightmare(bool breakInvariant = false)
	{
		var nightmare1 = new Nightmare();
		nightmare1.List = new();

		var nightmare2 = new Nightmare();
		nightmare2.List = new();

		nightmare1.List.Add(nightmare2);
		nightmare2.List.Add(nightmare1);

		foreach (var i in Enumerable.Range(0, 100))
		{
			var n1a = new Nightmare { List = [] };
			var n1b = new Nightmare { List = [] };
			var n2a = new Nightmare { List = [] };
			var n2b = new Nightmare { List = [] };

			n1a.List.Add(n1a);
			n1a.List.Add(n1b);
			n1a.List.Add(n2a);
			n1a.List.Add(n2b);
			n1a.List.Add(nightmare1.List.First());
			n1a.List.Add(nightmare1.List.Last());
			n1a.List.Add(nightmare2.List.First());
			n1a.List.Add(nightmare2.List.Last());
			nightmare1.List.Add(n1a);
			nightmare2.List.Add(n1a);

			n1b.List.Add(n1a);
			n1b.List.Add(n1b);
			n1b.List.Add(n2a);
			n1b.List.Add(n2b);
			n1b.List.Add(nightmare1.List.First());
			n1b.List.Add(nightmare1.List.Last());
			n1b.List.Add(nightmare2.List.First());
			n1b.List.Add(nightmare2.List.Last());
			nightmare1.List.Add(n1b);
			nightmare2.List.Add(n1b);

			n2a.List.Add(n1a);
			n2a.List.Add(n1b);
			n2a.List.Add(n2a);
			n2a.List.Add(n2b);
			n2a.List.Add(nightmare1.List.First());
			n2a.List.Add(nightmare1.List.Last());
			n2a.List.Add(nightmare2.List.First());
			n2a.List.Add(nightmare2.List.Last());
			nightmare1.List.Add(n2a);
			nightmare2.List.Add(n2a);

			n2b.List.Add(n1a);
			n2b.List.Add(n1b);
			n2b.List.Add(n2a);
			n2b.List.Add(n2b);
			n2b.List.Add(nightmare1.List.First());
			n2b.List.Add(nightmare1.List.Last());
			n2b.List.Add(nightmare2.List.First());
			n2b.List.Add(nightmare2.List.Last());
			nightmare1.List.Add(n2b);
			nightmare2.List.Add(n2b);

			if (i == 50 && breakInvariant)
			{
				n2b.String = null!;
			}
		}

		var outerNightmare = new Nightmare()
		{
			List = [nightmare1, nightmare2]
		};

		return outerNightmare;
	}

	private const int ZeroRecursion = 0;
	private const int FreeRecursion = int.MaxValue;
	private const long ZeroEnumeration = 0;
	private const long FreeEnumeration = long.MaxValue;
	private const bool BreakInvariants = true;
	private const bool KeepsInvariants = false;

	[Test]
	public void RecursiveEnumerativeSpaghetti()
	{
		var n = CreateNightmare(BreakInvariants);
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(n, ZeroRecursion, ZeroEnumeration));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(n, ZeroRecursion, FreeEnumeration));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(n, FreeRecursion, ZeroEnumeration));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(n, FreeRecursion, FreeEnumeration));

		n = CreateNightmare(KeepsInvariants);
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(n, ZeroRecursion, ZeroEnumeration));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(n, ZeroRecursion, FreeEnumeration));
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(n, FreeRecursion, ZeroEnumeration));

		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(n, FreeRecursion, FreeEnumeration));
	}
}


namespace DO_NOT_THROW_When
{
public class Required_members_ARE_SET_and
{
	[Test]
	public void Optional_members_ARE_SET()
	{
		Assert.DoesNotThrow(() =>
		{
			var t = TestObjects.Tree.FullyGrown();
			FF.AssertNullableInvariants(t);
		});
	}

	[Test]
	public void Optional_members_ARE_NOT_set()
	{
		Assert.DoesNotThrow(() =>
		{
			var t = TestObjects.Tree.Sapling();
			FF.AssertNullableInvariants(t);
		});
	}
}
}

namespace DO_THROW_When
{
public class OuterObject
{
	[Test]
	public static void IsNullReference()
	{
		Assert.Throws<InvariantException>(() =>
		{
			string s = null!;
			FF.AssertNullableInvariants(s);
		});
	}

	[Test]
	public static void IsNullStruct()
	{
		Assert.Throws<InvariantException>(() =>
		{
			int? i = null;
			FF.AssertNullableInvariants(i);
		});
	}

	[Test]
	public static void IsNull()
	{
		Assert.Throws<InvariantException>(() => { FF.AssertNullableInvariants((object)null!); });
	}
}

public class Required_members_are_NOT_SET_and
{
	[Test]
	public void Optional_members_ARE_SET()
	{
		Assert.Throws<InvariantException>(() =>
		{
			var t = TestObjects.Tree.Pruned();
			t.OptFld = TestObjects.Branch.FullyGrown();
			t.OptPrp = TestObjects.Branch.FullyGrown();
			FF.AssertNullableInvariants(t);
		});
	}

	[Test]
	public void Optional_members_ARE_NOT_set()
	{
		Assert.Throws<InvariantException>(() =>
		{
			var t = TestObjects.Tree.Pruned();
			FF.AssertNullableInvariants(t);
		});
	}
}
}

namespace FieldsAndProperties
{
public class Do_NOT_throw
{
	[Test]
	public void just_because_object_has_an_index_property()
	{
		var l = new List<int?>();
		l.Add(1);
		l.Add(null);

		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(l));
	}
}

public class Do_NOT_throw_when_all_required_are_set_and
{
	[Test]
	public void optional_field_and_optional_property_is_set()
	{
		var b = TestObjects.Bush.FullyGrown();
		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(b));
	}

	[Test]
	public void optional_field_is_set()
	{
		var b = TestObjects.Bush.FullyGrown();
		b.OptionalProperty = null;
		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(b));
	}

	[Test]
	public void optional_property_is_set()
	{
		var b = TestObjects.Bush.FullyGrown();
		b.OptionalField = null;
		Assert.DoesNotThrow(() => FF.AssertNullableInvariants(b));
	}
}

public class DO_THROW_When
{
	[Test]
	public void Required_property_is_not_set()
	{
		var b = TestObjects.Bush.FullyGrown();
		b.RequiredProperty = null!;
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(b));
	}

	[Test]
	public void Required_field_is_not_set()
	{
		var b = TestObjects.Bush.FullyGrown();
		b.RequiredField = null!;
		Assert.Throws<InvariantException>(() => FF.AssertNullableInvariants(b));
	}
}
}

public class TestObjects
{
	public class Bush
	{
		public Leaf RequiredProperty { get; set; } = new();
		public Leaf? OptionalProperty { get; set; } = new();
		public Leaf RequiredField = new();
		public Leaf? OptionalField = new();

		public static Bush FullyGrown() => new Bush();
	}

	public class Leaf
	{
	}

	public class Branch
	{
		public Leaf? OptPrp { get; set; }
		public Leaf ReqPrp { get; set; } = null!;
		public Leaf? OptFld = null;
		public Leaf ReqFld = null!;

		public static Branch FullyGrown()
		{
			return new Branch
			{
				OptPrp = new Leaf(),
				ReqPrp = new Leaf(),
				OptFld = new Leaf(),
				ReqFld = new Leaf()
			};
		}

		public static Branch Sapling()
		{
			return new Branch
			{
				ReqFld = new(),
				ReqPrp = new(),
			};
		}

		public static Branch Pruned()
		{
			return new Branch();
		}
	}

	public class Tree
	{
		public Branch? OptPrp { get; set; }
		public Branch ReqPrp { get; set; } = null!;
		public Branch? OptFld = null;
		public Branch ReqFld = null!;


		public static Tree FullyGrown()
		{
			return new Tree
			{
				OptPrp = Branch.FullyGrown(),
				ReqPrp = Branch.FullyGrown(),
				OptFld = Branch.FullyGrown(),
				ReqFld = Branch.FullyGrown()
			};
		}

		public static Tree Sapling()
		{
			return new Tree
			{
				ReqPrp = Branch.Sapling(),
				ReqFld = Branch.Sapling(),
			};
		}

		public static Tree Pruned()
		{
			return new Tree();
		}
	}


	[Test]
	public void Tree_Has04Branches_Has16Leaves_WhenComplete()
	{
		var t = Tree.FullyGrown();

		var branchCount = 0;

		if (t.ReqPrp != null) ++branchCount;
		if (t.ReqFld != null) ++branchCount;
		if (t.OptPrp != null) ++branchCount;
		if (t.OptFld != null) ++branchCount;

		Assert.That(branchCount, Is.EqualTo(4));

		var leafCount = 0;

		if (t.OptPrp?.OptPrp != null) ++leafCount;
		if (t.OptPrp?.OptFld != null) ++leafCount;
		if (t.OptPrp?.ReqPrp != null) ++leafCount;
		if (t.OptPrp?.ReqFld != null) ++leafCount;
		if (t.OptFld?.OptPrp != null) ++leafCount;
		if (t.OptFld?.OptFld != null) ++leafCount;
		if (t.OptFld?.ReqPrp != null) ++leafCount;
		if (t.OptFld?.ReqFld != null) ++leafCount;
		if (t.ReqPrp?.OptPrp != null) ++leafCount;
		if (t.ReqPrp?.OptFld != null) ++leafCount;
		if (t.ReqPrp?.ReqPrp != null) ++leafCount;
		if (t.ReqPrp?.ReqFld != null) ++leafCount;
		if (t.ReqFld?.OptPrp != null) ++leafCount;
		if (t.ReqFld?.OptFld != null) ++leafCount;
		if (t.ReqFld?.ReqPrp != null) ++leafCount;
		if (t.ReqFld?.ReqFld != null) ++leafCount;

		Assert.That(leafCount, Is.EqualTo(16));
	}

	[Test]
	public void Tree_Has02Branches_Has04Leaves_WhenPruned()
	{
		var t = Tree.Sapling();

		Assert.Null(t.OptPrp, "sapling tree should not have optional branch property");
		Assert.Null(t.OptFld, "sapling tree should not have optional branch field");

		Assert.NotNull(t.ReqPrp, "sapling tree should still have required branch property");
		Assert.NotNull(t.ReqFld, "sapling tree should still have required branch field");

		Assert.Null(t.ReqPrp.OptPrp);
		Assert.Null(t.ReqPrp.OptFld);
		Assert.Null(t.ReqFld.OptPrp);
		Assert.Null(t.ReqFld.OptFld);

		var branchCount = 0;

		if (t.ReqPrp != null) ++branchCount;
		if (t.ReqFld != null) ++branchCount;
		if (t.OptPrp != null) ++branchCount;
		if (t.OptFld != null) ++branchCount;

		Assert.That(branchCount, Is.EqualTo(2));

		var leafCount = 0;

		if (t.OptPrp?.OptPrp != null) ++leafCount;
		if (t.OptPrp?.OptFld != null) ++leafCount;
		if (t.OptPrp?.ReqPrp != null) ++leafCount;
		if (t.OptPrp?.ReqFld != null) ++leafCount;
		if (t.OptFld?.OptPrp != null) ++leafCount;
		if (t.OptFld?.OptFld != null) ++leafCount;
		if (t.OptFld?.ReqPrp != null) ++leafCount;
		if (t.OptFld?.ReqFld != null) ++leafCount;
		if (t.ReqPrp?.OptPrp != null) ++leafCount;
		if (t.ReqPrp?.OptFld != null) ++leafCount;
		if (t.ReqPrp?.ReqPrp != null) ++leafCount;
		if (t.ReqPrp?.ReqFld != null) ++leafCount;
		if (t.ReqFld?.OptPrp != null) ++leafCount;
		if (t.ReqFld?.OptFld != null) ++leafCount;
		if (t.ReqFld?.ReqPrp != null) ++leafCount;
		if (t.ReqFld?.ReqFld != null) ++leafCount;

		Assert.That(leafCount, Is.EqualTo(4));
	}

	[Test]
	public static void InvariantExceptionGathersNames()
	{
		var ie = new InvariantException(InvariantException.Reason.IllegalNullable);

		Assert.That(ie.Message, Is.EqualTo("Unspecified invariant error {0}"));

		ie.PushNameOfCurrentContext("Leaf");
		Assert.That(ie.Message, Is.EqualTo("Non-nullable reference Leaf is null"));

		ie.PushNameOfCurrentContext("Branch");
		Assert.That(ie.Message, Is.EqualTo("Non-nullable reference Branch.Leaf is null"));

		ie.PushNameOfCurrentContext("Trunk");
		Assert.That(ie.Message, Is.EqualTo("Non-nullable reference Trunk.Branch.Leaf is null"));
	}
}
}