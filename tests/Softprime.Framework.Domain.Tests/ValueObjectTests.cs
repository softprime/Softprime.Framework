using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Softprime.Framework.Domain.Tests
{
    public class ValueObjectTests
    {
        [Fact]
        public void Equals_PublicPropertiesIncludingNull_DefineValueEquality()
        {
            var first = new Measurement { Length = 12 };
            var same = new Measurement { Length = 12 };
            var differentLength = new Measurement { Length = 13 };
            var differentUnit = new Measurement { Length = 12, Unit = "cm" };
            var otherType = new DerivedMeasurement { Length = 12 };
            ValueObject absent = null;

            first.Equals(same).Should().BeTrue();
            (first == same).Should().BeTrue();
            first.GetHashCode().Should().Be(same.GetHashCode());
            (first != differentLength).Should().BeTrue();
            first.Equals(differentUnit).Should().BeFalse();
            first.Equals(otherType).Should().BeFalse();
            (absent == (ValueObject)null).Should().BeTrue();
            (absent != first).Should().BeTrue();
            (first == absent).Should().BeFalse();
        }

        [Fact]
        public void Equals_ObjectsWithoutPublicProperties_UsesReferenceIdentity()
        {
            var first = new EmptyValue();
            var second = new EmptyValue();

            first.Equals(first).Should().BeTrue();
            first.Equals(second).Should().BeFalse();
        }

        [Fact]
        public void Distinct_WithDomainComparer_UsesValueEqualityAndHashCode()
        {
            var first = new Measurement { Length = 1, Unit = "m" };
            var copy = new Measurement { Length = 1, Unit = "m" };
            var different = new Measurement { Length = 1, Unit = "cm" };
            var comparer = new BaseObjectEqualityComparer<Measurement>();

            var distinct = new[] { first, copy, different }.Distinct(comparer).ToArray();

            distinct.Should().HaveCount(2);
            distinct[0].Should().BeSameAs(first);
            distinct[1].Should().BeSameAs(different);
            comparer.Equals(null, null).Should().BeTrue();
            comparer.Equals(first, null).Should().BeFalse();
        }

        [Fact]
        public void GetSignatureProperties_AnnotatedProperty_Throws()
        {
            var value = new InvalidMeasurement();

            Action readSignature = () => value.GetSignatureProperties();

            readSignature.Should().Throw<InvalidOperationException>().WithMessage("*DomainSignature*");
        }

        private class Measurement : ValueObject
        {
            public int Length { get; set; }
            public string Unit { get; set; }
        }

        private sealed class DerivedMeasurement : Measurement
        {
        }

        private sealed class EmptyValue : ValueObject
        {
        }

        private sealed class InvalidMeasurement : ValueObject
        {
            [DomainSignature]
            public int Length { get; set; }
        }
    }
}
