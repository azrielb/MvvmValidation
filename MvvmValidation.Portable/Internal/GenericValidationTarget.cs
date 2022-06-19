﻿using System;
using System.Collections.Generic;

namespace MvvmValidation.Internal
{
    internal class GenericValidationTarget : IValidationTarget, IEquatable<GenericValidationTarget>
    {
        public GenericValidationTarget(object targetId)
        {
            Guard.NotNull(targetId, nameof(targetId));

            TargetId = targetId;
        }

        public object TargetId { get; set; }

        #region IEquatable<GenericValidationTarget> Members

        public bool Equals(GenericValidationTarget other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Equals(other.TargetId, TargetId);
        }

        #endregion

        #region IValidationTarget Members

        public IEnumerable<object> UnwrapTargets()
        {
            return new[] { TargetId };
        }

        public bool IsMatch(object target)
        {
            return Equals(target, TargetId);
        }

        #endregion

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
            if (obj.GetType() != typeof(GenericValidationTarget))
            {
                return false;
            }
            return Equals((GenericValidationTarget) obj);
        }

        public override int GetHashCode()
        {
            return TargetId != null ? TargetId.GetHashCode() : 0;
        }
    }
}