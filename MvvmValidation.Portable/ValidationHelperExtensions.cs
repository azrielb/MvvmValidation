﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MvvmValidation.Internal;

namespace MvvmValidation
{
    /// <summary>
    /// Contains extensions methods for <see cref="ValidationHelper"/>.
    /// </summary>
    public static class ValidationHelperExtensions
    {
        /// <summary>
        /// Adds a rule that checks that the property represented by <paramref name="propertyExpression"/> is not
        /// null or empty string. 
        /// </summary>
        /// <param name="validator">An instance of <see cref="ValidationHelper"/> that is used for validation.</param>
        /// <param name="propertyExpression">Expression that specifies the property to validate. Example: Validate(() => MyProperty).</param>
        /// <param name="errorMessage">Error message in case if the property is null or empty.</param>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public static IValidationRule AddRequiredRule([NotNull] this ValidationHelper validator,
            [NotNull] Expression<Func<object>> propertyExpression, [NotNull] string errorMessage)
        {
            Guard.NotNull(validator, nameof(validator));
            Guard.NotNull(propertyExpression, nameof(propertyExpression));
            Guard.NotNullOrEmpty(errorMessage, nameof(errorMessage));

            var propertyGetter = propertyExpression.Compile();

            return validator.AddRule(PropertyName.For(propertyExpression, false), () =>
            {
                object propertyValue = propertyGetter();

                string stringPropertyValue = propertyValue as string;

                if (propertyValue == null || (stringPropertyValue != null && string.IsNullOrEmpty(stringPropertyValue)))
                {
                    return RuleResult.Invalid(errorMessage);
                }

                return RuleResult.Valid();
            });
        }

        /// <summary>
        /// Creates a validation rule that validates the specified child <see cref="IValidatable"/> object and adds errors
        /// to this object if invalid.
        /// </summary>
        /// <param name="validator">An instance of <see cref="ValidationHelper"/> that is used for validation.</param>
        /// <param name="childValidatableGetter">Expression for getting the <see cref="IValidatable"/> object to add as child.</param>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public static IAsyncValidationRule AddChildValidatable([NotNull] this ValidationHelper validator,
            [NotNull] Expression<Func<IValidatable>> childValidatableGetter)
        {
            Guard.NotNull(validator, nameof(validator));
            Guard.NotNull(childValidatableGetter, nameof(childValidatableGetter));

            var getter = childValidatableGetter.Compile();

            return validator.AddAsyncRule(PropertyName.For(childValidatableGetter), () =>
            {
                var validatable = getter();

                if (validatable != null)
                {
                    return validatable.Validate().ContinueWith(r =>
                    {
                        var result = r.Result;

                        var ruleResult = new RuleResult();

                        foreach (var error in result.ErrorList)
                        {
                            ruleResult.AddError(error.ErrorText);
                        }

                        return ruleResult;
                    });
                }

                return TaskEx.FromResult(RuleResult.Valid());
            });
        }

        /// <summary>
        /// Creates a validation rule that validates all the <see cref="IValidatable"/> items in the collection specified in <paramref name="validatableCollectionGetter"/>
        /// and adds error to this object from all the validatable items in invalid.
        /// </summary>
        /// <param name="validator">An instance of <see cref="ValidationHelper"/> that is used for validation.</param>
        /// <param name="validatableCollectionGetter">Expression for getting the collection of <see cref="IValidatable"/> objects to add as child items.</param>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public static IAsyncValidationRule AddChildValidatableCollection([NotNull] this ValidationHelper validator,
            [NotNull] Expression<Func<IEnumerable<IValidatable>>> validatableCollectionGetter)
        {
            Guard.NotNull(validator, nameof(validator));
            Guard.NotNull(validatableCollectionGetter, nameof(validatableCollectionGetter));

            var getter = validatableCollectionGetter.Compile();

            return validator.AddAsyncRule(PropertyName.For(validatableCollectionGetter), () =>
            {
                var items = getter();

                if (items == null)
                {
                    return TaskEx.FromResult(RuleResult.Valid());
                }

                items = items as IValidatable[] ?? items.ToArray();

                if (!items.Any())
                {
                    return TaskEx.FromResult(RuleResult.Valid());
                }

                var result = new RuleResult();

                // Execute validation on all items at the same time, wait for all
                // to finish and combine the results.

                var results = new List<ValidationResult>();

                var tasks = new List<Task<ValidationResult>>();

                foreach (var item in items)
                {
                    var task = item.Validate().ContinueWith(tr =>
                    {
                        var r = tr.Result;
                        var ex = tr.Exception;

                        lock (results)
                        {
                            if (ex == null && r != null)
                            {
                                results.Add(r);
                            }
                        }

                        return tr.Result;
                    });

                    tasks.Add(task);
                }

                var resultTask = TaskEx.WhenAll(tasks).ContinueWith(tr =>
                {
                    if (tr.Exception == null)
                    {
                        // Add errors from all validation results
                        foreach (var itemResult in results)
                        {
                            foreach (var error in itemResult.ErrorList)
                            {
                                result.AddError(error.ErrorText);
                            }
                        }

                        return result;
                    }

                    throw new AggregateException(tr.Exception);
                });

                return resultTask;
            });
        }
    }
}