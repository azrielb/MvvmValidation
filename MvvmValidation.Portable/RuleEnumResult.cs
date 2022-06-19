using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MvvmValidation.Internal;

namespace MvvmValidation
{
    /// <summary>
    /// Represents the outcome of a validation rule when executed.
    /// </summary>
    public class RuleEnumResult<E> : RuleResult, IEquatable<RuleEnumResult<E>>, IEquatable<RuleResult> where E : Enum
    {
        private readonly IList<E> errors;

        #region Factory Methods

        /// <summary>
        /// Creates an "Invalid" result with the given error <paramref name="error"/>.
        /// </summary>
        /// <param name="error">The error text that describes why this rule is invalid.</param>
        /// <returns>An instance of <see cref="RuleEnumResult{E}"/> that represents an invalid result.</returns>
        [NotNull]
        public static RuleEnumResult<E> Invalid([NotNull] E error)
        {
            //Guard.NotNullOrEmpty(error, nameof(error));
            return new RuleEnumResult<E>(error);
        }

        /// <summary>
        /// Creates a "Valid" result.
        /// </summary>
        /// <returns>An instance of <see cref="RuleEnumResult{E}"/> that represents a valid outcome of the rule.</returns>
        [NotNull]
        public static new RuleEnumResult<E> Valid()
        {
            return new RuleEnumResult<E>();
        }

        /// <summary>
        /// Asserts the specified condition and if <c>false</c> then creates and invalid result with the given <paramref name="errorMessage"/>. 
        /// If <c>true</c>, returns a valid result.
        /// </summary>
        /// <param name="condition">The assertion.</param>
        /// <param name="errorMessage">The error message in case if the <paramref name="condition"/> is not <c>true</c>.</param>
        /// <returns>An instance of <see cref="RuleEnumResult{E}"/> that represents the result of the assertion.</returns>
        [NotNull]
        public static RuleEnumResult<E> Assert(bool condition, [NotNull] E errorMessage)
        {
            //Guard.NotNullOrEmpty(errorMessage, nameof(errorMessage));

            if (!condition)
            {
                return Invalid(errorMessage);
            }

            return Valid();
        }

        #endregion

        /// <summary>
        /// Creates an empty (valid) instance of <see cref="RuleEnumResult{E}"/>. 
        /// The <see cref="AddError"/> method can be used to add errors to the result later.
        /// </summary>
        public RuleEnumResult()
            : this(true, new E[] { })
        {
        }

        private RuleEnumResult(E error)
            : this(false, new[] { error })
        {
            //Guard.NotNullOrEmpty(error, nameof(error));
        }

        private RuleEnumResult(bool isValid, IEnumerable<E> errors)
        {
            Guard.NotNull(errors, nameof(errors));

            IsValid = isValid;

            this.errors = new List<E>(errors);
        }

        /// <summary>
        /// Gets the error messages in case if the target is invalid according to this validation rule.
        /// </summary>
        [NotNull]
        public new IEnumerable<string> Errors
        {
            get
            {
                return errors.Select(x => x.ToString());
            }
        }
        /// <summary>
        /// Gets the errors in case if the target is invalid according to this validation rule.
        /// </summary>
        [NotNull]
        public IEnumerable<E> ErrorValues
        {
            get
            {
                return errors;
            }
        }

        /// <summary>
        /// Adds an error to the result.
        /// </summary>
        /// <param name="error">The error message to add.</param>
        public void AddError([NotNull] E error)
        {
            //Guard.NotNullOrEmpty(error, nameof(error));

            errors.Add(error);
            IsValid = false;
        }

        #region Equality Members

        #region IEquatable<RuleEnumResult> Members

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        public bool Equals(RuleEnumResult<E> other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return other.errors.ItemsEqual(errors) && other.IsValid.Equals(IsValid);
        }

        #endregion
        #region IEquatable<RuleResult> Members

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        public new bool Equals(RuleResult other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return other.IsValid.Equals(IsValid) && other.Errors.ItemsEqual(Errors);
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
            if (obj.GetType() == typeof(RuleEnumResult<E>))
            {
                return Equals((RuleEnumResult<E>) obj);
            }
            if (obj.GetType() == typeof(RuleResult))
            {
                return Equals((RuleResult) obj);
            }
            return false;
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
                return ((errors?.GetHashCode() ?? 0) * 397) ^ IsValid.GetHashCode();
            }
        }

        #endregion
    }
}