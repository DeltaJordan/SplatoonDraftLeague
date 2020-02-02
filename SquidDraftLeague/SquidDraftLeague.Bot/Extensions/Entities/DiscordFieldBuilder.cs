using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Bot.Extensions.Entities
{
    /// <summary>
    ///     Represents a builder class for an embed field.
    /// </summary>
    public class DiscordFieldBuilder
    {
        private string _name;
        private string _value;
        /// <summary>
        ///     Gets the maximum field length for name allowed by Discord.
        /// </summary>
        public const int MaxFieldNameLength = 256;
        /// <summary>
        ///     Gets the maximum field length for value allowed by Discord.
        /// </summary>
        public const int MaxFieldValueLength = 1024;

        /// <summary>
        ///     Gets or sets the field name.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <para>Field name is <c>null</c>, empty or entirely whitespace.</para>
        /// <para><c>- or -</c></para>
        /// <para>Field name length exceeds <see cref="MaxFieldNameLength"/>.</para>
        /// </exception>
        /// <returns>
        ///     The name of the field.
        /// </returns>
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message: "Field name must not be null, empty or entirely whitespace.", paramName: nameof(Name));
                if (value.Length > MaxFieldNameLength) throw new ArgumentException(message: $"Field name length must be less than or equal to {MaxFieldNameLength}.", paramName: nameof(Name));
                _name = value;
            }
        }

        /// <summary>
        ///     Gets or sets the field value.
        /// </summary>
        /// <exception cref="ArgumentException" accessor="set">
        /// <para>Field value is <c>null</c>, empty or entirely whitespace.</para>
        /// <para><c>- or -</c></para>
        /// <para>Field value length exceeds <see cref="MaxFieldValueLength"/>.</para>
        /// </exception>
        /// <returns>
        ///     The value of the field.
        /// </returns>
        public object Value
        {
            get => _value;
            set
            {
                var stringValue = value?.ToString();
                if (string.IsNullOrWhiteSpace(stringValue)) throw new ArgumentException(message: "Field value must not be null or empty.", paramName: nameof(Value));
                if (stringValue.Length > MaxFieldValueLength) throw new ArgumentException(message: $"Field value length must be less than or equal to {MaxFieldValueLength}.", paramName: nameof(Value));
                _value = stringValue;
            }
        }
        /// <summary>
        ///     Gets or sets a value that indicates whether the field should be in-line with each other.
        /// </summary>
        public bool IsInline { get; set; }

        /// <summary>
        ///     Sets the field name.
        /// </summary>
        /// <param name="name">The name to set the field name to.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public DiscordFieldBuilder WithName(string name)
        {
            Name = name;
            return this;
        }
        /// <summary>
        ///     Sets the field value.
        /// </summary>
        /// <param name="value">The value to set the field value to.</param>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public DiscordFieldBuilder WithValue(object value)
        {
            Value = value;
            return this;
        }
        /// <summary>
        ///     Determines whether the field should be in-line with each other.
        /// </summary>
        /// <returns>
        ///     The current builder.
        /// </returns>
        public DiscordFieldBuilder WithIsInline(bool isInline)
        {
            IsInline = isInline;
            return this;
        }
    }
}
