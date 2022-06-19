﻿using System;
using JetBrains.Annotations;
using MvvmValidation.Internal;

namespace MvvmValidation
{
    /// <summary>
    /// Represents a validation error.
    /// </summary>
    public class ValidationError : IEquatable<ValidationError>
    {
        internal ValidationError([NotNull] string errorText, [NotNull] object target)
        {
            Guard.NotNullOrEmpty(errorText, nameof(errorText));
            Guard.NotNull(target, nameof(target));

            ErrorText = errorText;
            Target = target;
        }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        [NotNull]
        public string ErrorText { get; }

        /// <summary>
        /// Gets the target of the error (a property name or any other arbitrary object).
        /// </summary>
        [NotNull]
        public object Target { get; }

        #region IEquatable<ValidationError> Members

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        public bool Equals(ValidationError other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Equals(other.ErrorText, ErrorText) && Equals(other.Target, Target);
        }

        #endregion

        /// <summary>
        /// Determines whether the specified <see cref="object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != typeof(ValidationError))
            {
                return false;
            }
            return Equals((ValidationError) obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int result = ErrorText.GetHashCode();
                result = (result * 397) ^ Target.GetHashCode();
                result *= 397;
                return result;
            }
        }

        /// <summary>
        /// Returns a <see cref="string"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return ErrorText;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="MvvmValidation.ValidationError"/> to <see cref="string"/>.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator string(ValidationError error)
        {
            Guard.NotNull(error, nameof(error));

            return error.ToString();
        }
    }
}