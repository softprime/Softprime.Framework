using System;
using System.Collections.Generic;
using FluentAssertions;
using Softprime.Framework.Domain.DAL;
using Xunit;

namespace Softprime.Framework.Domain.Tests
{
    public class EntityTests
    {
        [Fact]
        public void IsTransient_DefaultAndAssignedKeys_FollowsTheIdTypeDefault()
        {
            AssertTransient(0, true);
            AssertTransient(7, false);
            AssertTransient(Guid.Empty, true);
            AssertTransient(Guid.Parse("7768b6cd-30e4-441d-84a3-b50a264ff301"), false);
            AssertTransient<string>(null, true);
            AssertTransient(string.Empty, false);
            AssertTransient("key", false);
            AssertTransient<long?>(null, true);
            AssertTransient<long?>(0, false);
            AssertTransient<long?>(7, false);

            var entity = new HookEntity();
            entity.IsTransient().Should().BeTrue();
            entity.SetId(0);
            entity.IsTransient().Should().BeFalse();
        }

        [Fact]
        public void Equals_TransientWithoutSignature_UsesReferenceIdentity()
        {
            var first = new TestEntity<int>();
            var another = new TestEntity<int>();

            first.Equals(first).Should().BeTrue();
            first.Equals(another).Should().BeFalse();
            first.Equals(null).Should().BeFalse();
            first.Equals("other").Should().BeFalse();
        }

        [Fact]
        public void Equals_TransientWithSignature_UsesAnnotatedPropertyAndConcreteType()
        {
            var first = new SignedEntity { Code = "one", Label = "first" };
            var same = new SignedEntity { Code = "one", Label = "second" };
            var different = new SignedEntity { Code = "two" };
            var derived = new DerivedEntity { Code = "one" };

            first.Equals(same).Should().BeTrue();
            first.GetHashCode().Should().Be(same.GetHashCode());
            (first == same).Should().BeFalse();
            (first != same).Should().BeTrue();
            first.Equals(different).Should().BeFalse();
            first.Equals(derived).Should().BeFalse();
            derived.Equals(first).Should().BeFalse();
        }

        [Fact]
        public void Equals_PersistedEntities_UsesSameIdAndConcreteType()
        {
            var first = new SignedEntity { Code = "one" };
            var sameId = new SignedEntity { Code = "different" };
            var otherId = new SignedEntity { Code = "one" };
            var transient = new SignedEntity { Code = "one" };
            var derived = new DerivedEntity { Code = "one" };
            first.SetId(7);
            sameId.SetId(7);
            otherId.SetId(8);
            derived.SetId(7);

            first.Equals(sameId).Should().BeTrue();
            first.GetHashCode().Should().Be(sameId.GetHashCode());
            first.Equals(otherId).Should().BeFalse();
            first.Equals(transient).Should().BeFalse();
            transient.Equals(first).Should().BeFalse();
            first.Equals(derived).Should().BeFalse();
        }

        [Fact]
        public void GetHashCode_SignatureAndIdChange_PreservesCollectionLookups()
        {
            var entity = new SignedEntity { Code = "initial" };
            var hash = entity.GetHashCode();
            var set = new HashSet<SignedEntity> { entity };
            var dictionary = new Dictionary<SignedEntity, string> { [entity] = "stored" };

            entity.Code = "changed";
            entity.GetHashCode().Should().Be(hash);
            entity.SetId(7);

            entity.GetHashCode().Should().Be(hash);
            set.Contains(entity).Should().BeTrue();
            dictionary.TryGetValue(entity, out var value).Should().BeTrue();
            value.Should().Be("stored");
        }

        [Fact]
        public void Hooks_ExplicitCalls_DispatchToOverrides()
        {
            var observed = new HookEntity();
            Entity entity = observed;

            entity.BeforeSave();
            entity.AfterSave();
            entity.BeforeDelete();
            entity.AfterDelete();

            observed.Calls.Should().Equal("BeforeSave", "AfterSave", "BeforeDelete", "AfterDelete");
        }

        private static void AssertTransient<T>(T id, bool expected)
        {
            var entity = new TestEntity<T>();

            entity.SetId(id);

            entity.IsTransient().Should().Be(expected);
        }

        private class TestEntity<T> : EntityWithTypedId<T>
        {
        }

        private class SignedEntity : TestEntity<int>
        {
            [DomainSignature]
            public string Code { get; set; }

            public string Label { get; set; }
        }

        private sealed class DerivedEntity : SignedEntity
        {
        }

        private sealed class HookEntity : Entity
        {
            public List<string> Calls { get; } = new List<string>();

            public override void BeforeSave()
            {
                base.BeforeSave();
                Calls.Add(nameof(BeforeSave));
            }

            public override void AfterSave()
            {
                base.AfterSave();
                Calls.Add(nameof(AfterSave));
            }

            public override void BeforeDelete()
            {
                base.BeforeDelete();
                Calls.Add(nameof(BeforeDelete));
            }

            public override void AfterDelete()
            {
                base.AfterDelete();
                Calls.Add(nameof(AfterDelete));
            }
        }
    }
}
