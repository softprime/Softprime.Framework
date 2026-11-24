using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        public void Equals_NullDomainSignature_StillComparesTheSignature()
        {
            var first = new SignedEntity();
            var same = new SignedEntity();
            var named = new SignedEntity { Code = "named" };

            first.HasSameObjectSignatureAs(same).Should().BeTrue();
            first.Equals(same).Should().BeTrue();
            first.GetHashCode().Should().Be(same.GetHashCode());
            first.Equals(named).Should().BeFalse();
            named.Equals(first).Should().BeFalse();
        }

        [Fact]
        public void GetHashCode_EntitiesEqualAfterIdAssignment_CanHaveDifferentCachedHashes()
        {
            var previouslyTransient = new NumberedEntity { Number = 1 };
            var fresh = new NumberedEntity { Number = 1 };
            var transientHash = previouslyTransient.GetHashCode();

            previouslyTransient.SetId(2);
            fresh.SetId(2);

            previouslyTransient.Equals(fresh).Should().BeTrue();
            previouslyTransient.GetHashCode().Should().Be(transientHash);
            fresh.GetHashCode().Should().NotBe(transientHash);
        }

        [Fact]
        public void Equals_EntityAdvertisingTheUnproxiedType_IsDirectional()
        {
            var entity = new SignedEntity { Code = "same" };
            var proxy = new PretendProxy { Code = "same" };
            entity.SetId(4);
            proxy.SetId(4);

            entity.Equals(proxy).Should().BeTrue();
            proxy.Equals(entity).Should().BeFalse();
        }

        [Theory]
        [InlineData(null, 1, nameof(ValidatingEntity.Name))]
        [InlineData("sample", 0, nameof(ValidatingEntity.Level))]
        [InlineData("sample", 11, nameof(ValidatingEntity.Level))]
        public void ValidationResults_InvalidAnnotatedProperty_IdentifiesTheMember(
            string name, int level, string invalidMember)
        {
            var entity = new ValidatingEntity { Name = name, Level = level };

            entity.IsValid().Should().BeFalse();
            entity.ValidationResults().Should().ContainSingle()
                .Which.MemberNames.Should().Equal(invalidMember);
        }

        [Fact]
        public void IsValid_AfterUpdatingInvalidProperties_ReevaluatesAnnotations()
        {
            var entity = new ValidatingEntity();
            entity.ValidationResults().Should().HaveCount(2);

            entity.Name = "sample";
            entity.Level = 1;

            entity.IsValid().Should().BeTrue();
            entity.ValidationResults().Should().BeEmpty();
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

        private sealed class NumberedEntity : TestEntity<int>
        {
            [DomainSignature]
            public int Number { get; set; }
        }

        private sealed class PretendProxy : SignedEntity
        {
            protected override Type GetTypeUnproxied() => typeof(SignedEntity);
        }

        private sealed class ValidatingEntity : TestEntity<int>
        {
            [Required]
            public string Name { get; set; }

            [Range(1, 10)]
            public int Level { get; set; }
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
