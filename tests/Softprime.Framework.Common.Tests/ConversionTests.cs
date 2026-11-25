using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Softprime.Framework.Common.Extensions;
using Softprime.Framework.Common.Helpers;
using Xunit;
using LegacyCompare = Softprime.Framework.Common.Annotations.CompareAttribute;

namespace Softprime.Framework.Common.Tests
{
    public sealed class ConversionTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("0", 0)]
        [InlineData("-25", -25)]
        public void ChangeTypeTo_NullableInteger_PreservesNullOrBoxesUnderlyingValue(string input, int? expected)
        {
            var converted = input.ChangeTypeTo<int?>();

            converted.Should().Be(expected);
        }

        [Fact]
        public void ChangeTypeTo_GuidFromText_ParsesGuidAndRejectsInvalidText()
        {
            const string text = "8f4b8795-d763-4a1e-b34d-d11605cb81ac";

            text.ChangeTypeTo<Guid>().Should().Be(Guid.Parse(text));
            Action convert = () => "invalid".ChangeTypeTo<Guid>();
            convert.Should().ThrowExactly<FormatException>();
        }

        [Fact]
        public void ChangeTypeTo_Int32ToInt64_ThrowsLegacySqliteDiagnostic()
        {
            object value = 42;

            Action convert = () => value.ChangeTypeTo<long>();

            convert.Should().ThrowExactly<InvalidOperationException>().WithMessage("*SQLite*");
            "42".ChangeTypeTo<long>().Should().Be(42L);
        }

        [Fact]
        public void CopyTo_SharedWritableProperties_UpdatesExistingObject()
        {
            var from = new { Name = "source", Code = "incoming", Extra = 7 };
            var to = new CopyTarget { Name = "old", Count = 9 };

            var result = from.CopyTo(to);

            result.Should().BeSameAs(to);
            to.Name.Should().Be("source");
            to.Count.Should().Be(9);
            to.Code.Should().Be("fixed");
        }

        [Fact]
        public void FromDictionary_UnexpectedPropertyType_DoesNotCoerceValue()
        {
            var settings = new Dictionary<string, object> { [nameof(CopyTarget.Count)] = "7" };

            Action assign = () => settings.FromDictionary(new CopyTarget());

            assign.Should().ThrowExactly<ArgumentException>();
        }

        [Theory]
        [InlineData("same", "same", true)]
        [InlineData("same", "SAME", false)]
        [InlineData(null, null, true)]
        [InlineData(null, "same", false)]
        public void Compare_WithExistingProperty_UsesCaseSensitiveObjectEquality(string expected, string actual, bool valid)
        {
            var input = new Confirmation { Original = expected, Repeated = actual };
            var context = new ValidationContext(input) { MemberName = nameof(Confirmation.Repeated) };
            var attribute = new LegacyCompare(nameof(Confirmation.Original)) { ErrorMessage = "{0} differs" };

            var result = attribute.GetValidationResult(input.Repeated, context);

            (result == ValidationResult.Success).Should().Be(valid);
            if (!valid)
                result.ErrorMessage.Should().Be("Repeated differs");
        }

        [Fact]
        public void Compare_MissingProperty_ReturnsValidationError()
        {
            var context = new ValidationContext(new Confirmation()) { MemberName = nameof(Confirmation.Repeated) };
            var attribute = new LegacyCompare("Unknown");

            var result = attribute.GetValidationResult("value", context);

            result.ErrorMessage.Should().Contain("Unknown");
        }

        [Fact]
        public void Distance_EquatorialDegree_ReturnsKilometresAndIsSymmetric()
        {
            var east = GeoHelper.Distance(0, 0, 0, 1);
            var west = GeoHelper.Distance(0, 1, 0, 0);

            east.Should().BeApproximately(111.29, 0.02);
            west.Should().BeApproximately(east, 0.001);
            GeoHelper.Distance(0, 1, 0, 1).Should().Be(0);
        }

        private sealed class CopyTarget
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public string Code => "fixed";
        }

        private sealed class Confirmation
        {
            public string Original { get; set; }
            public string Repeated { get; set; }
        }
    }
}
