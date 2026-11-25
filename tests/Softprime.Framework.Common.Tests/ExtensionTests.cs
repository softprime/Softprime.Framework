using System;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using FluentAssertions;
using Softprime.Framework.Common.Extensions;
using Softprime.Framework.Common.Helpers;
using Xunit;

namespace Softprime.Framework.Common.Tests
{
    public class ExtensionTests
    {
        [Theory]
        [InlineData("pt-BR")]
        [InlineData("en-US")]
        public void Strings_NullsWhitespaceAndUnicode_PreserveLegacyRules(string culture)
        {
            using var scope = new CultureScope(culture);
            string missing = null;

            missing.Matches(null).Should().BeTrue();
            missing.Matches("").Should().BeFalse();
            "Texto".Matches("texto").Should().BeTrue();
            " Texto ".Matches("texto").Should().BeFalse();
            " Texto ".MatchesTrimmed("texto").Should().BeTrue();
            Action trimNull = () => missing.MatchesTrimmed("");
            trimNull.Should().Throw<NullReferenceException>();
            "A1٢²9".OnlyNumbers().Should().Be("1٢9");
            missing.OnlyNumbers().Should().BeEmpty();
            "".OnlyNumbers().Should().BeEmpty();
            missing.RemoveAcentos().Should().BeEmpty();
            "".RemoveAcentos().Should().BeEmpty();
#if NET48
            "ação 東京".RemoveAcentos().Should().Be("acao ??");
#else
            Action removeAccents = () => "ação 東京".RemoveAcentos();
            removeAccents.Should().ThrowExactly<ArgumentException>().WithParameterName("name");
#endif
        }

        [Theory]
        [InlineData("pt-BR", "-1,25", "-1.25")]
        [InlineData("en-US", "-1.25", "-1,25")]
        public void Numeric_SeparatorsSignsAndPrecision_MatchTheCurrentCulture(
            string culture, string validDecimal, string invalidDecimal)
        {
            using var scope = new CultureScope(culture);

            "-25".IsInteger().Should().BeTrue();
            "+25".IsInteger().Should().BeFalse();
            " 25 ".IsInteger().Should().BeFalse();
            "".IsInteger().Should().BeFalse();
            validDecimal.IsInteger().Should().BeFalse();
            validDecimal.IsNumber().Should().BeTrue();
            invalidDecimal.IsNumber().Should().BeFalse();
            "+25".IsNumber().Should().BeTrue();
            ((string)null).IsNumber().Should().BeFalse();
            "text".IsNumber().Should().BeFalse();
            Action checkNull = () => ((string)null).IsInteger();
            checkNull.Should().Throw<ArgumentNullException>();
            1.239.Truncate(2).Should().Be(1.23);
            (-1.239).Truncate(2).Should().Be(-1.23);
            129d.Truncate(-1).Should().Be(120);
        }

        [Theory]
        [InlineData("pt-BR", "01/03/2024", "28/02/2024")]
        [InlineData("en-US", "03/01/2024", "02/28/2024")]
        public void Dates_LeapDayAndBirthday_UseTheCurrentParsingAndDayOfYearRules(
            string culture, string march, string february)
        {
            using var scope = new CultureScope(culture);
            var birth = new DateTime(2000, 2, 29);
            var leapDay = new DateTime(2024, 2, 29);

            march.DiffDays(february).Should().Be(2);
            february.DiffDays(march).Should().Be(-2);
            march.DiffDays("invalid").Should().Be(0);
            leapDay.AddHours(12).DiffDays(leapDay).Should().Be(0.5);
            leapDay.IsWeekDay().Should().BeTrue();
            leapDay.AddDays(2).IsWeekDay().Should().BeFalse();
            birth.GetAge(leapDay.AddDays(-1)).Should().Be(23);
            birth.GetAge(leapDay).Should().Be(23);
            birth.GetAge(leapDay.AddDays(1)).Should().Be(24);
        }

        [Fact]
        public void Linq_ConstantMemberAndUnsupportedExpressions_ReturnLegacyValues()
        {
            var captured = 7;
            Expression<Func<int>> literal = () => 42;
            Expression<Func<int>> member = () => captured;
            Expression<Func<object>> converted = () => captured;
            Expression<Func<int>> call = () => Math.Abs(captured);

            literal.Body.GetConstantValue().Should().Be(42);
            literal.ParseObjectValue().Should().BeEmpty();
            member.ParseObjectValue().Should().Be(nameof(captured));
            converted.ParseObjectValue().Should().Be(nameof(captured));
            member.Body.GetConstantValue().Should().BeNull();
            call.ParseObjectValue().Should().BeEmpty();
            call.Body.GetConstantValue().Should().BeNull();
        }

        [Fact]
        public void EnumStreamAndDataUtilities_ExposeRepresentativeResults()
        {
            using var stream = new MemoryStream(new byte[] { 3, 4, 5 });
            stream.Position = 1;
            using var table = new DataTable();
            table.Columns.Add("value", typeof(int));
            table.Rows.Add(8);
            table.Rows.Add(13);
            using var reader = table.CreateDataReader();

            State.On.GetDescription().Should().Be("Enabled");
            EnumHelper.GetValueFromDescription<State>("Enabled").Should().Be(State.On);
            EnumHelper.GetValueFromDescription<State>("unknown").Should().Be(State.Off);
            stream.ReadFully().Should().Equal(4, 5);
            reader.ToEnumerableValueType<int>().Should().Equal(8, 13);
            DbType.Int64.GetSqlDBType().Should().Be(SqlDbType.BigInt);
        }

        [Fact]
        public void UpdateFileText_Utf8File_ReplacesAllMatchesAndKeepsOtherCharacters()
        {
            var path = Path.Combine(Path.GetTempPath(), "softprime-common-" + Guid.NewGuid().ToString("N"));
            "ação antigo antigo".CreateToFile(path);

            try
            {
                path.UpdateFileText("antigo", "novo");

                path.GetFileText().Should().Be("ação novo novo");
            }
            finally
            {
                File.Delete(path);
            }
        }

        private enum State
        {
            Off,
            [Description("Enabled")]
            On
        }

        private sealed class CultureScope : IDisposable
        {
            private readonly CultureInfo previous = CultureInfo.CurrentCulture;
            private readonly CultureInfo previousUi = CultureInfo.CurrentUICulture;

            public CultureScope(string name)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            }

            public void Dispose()
            {
                CultureInfo.CurrentCulture = previous;
                CultureInfo.CurrentUICulture = previousUi;
            }
        }
    }
}
