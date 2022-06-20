using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MvvmValidation.Internal;

namespace MvvmValidation
{
    /// <summary>
    /// Main helper class that contains the functionality of managing validation rules, 
    /// executing validation using those rules and keeping validation results.
    /// </summary>
    public class ValidationHelper
    {
        #region Fields

        private readonly IDictionary<object, IDictionary<ValidationRule, RuleResult>> ruleValidationResultMap =
            new Dictionary<object, IDictionary<ValidationRule, RuleResult>>();

        private readonly AutoToggle suppressToggle = new AutoToggle(false);

        private readonly object syncRoot = new object();

        #endregion

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationHelper"/> class.
        /// </summary>
        public ValidationHelper()
            : this(new ValidationSettings())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationHelper"/> class.
        /// </summary>
        /// <param name="settings">An instance of <see cref="ValidationSettings"/> that control the behavior of this instance of <see cref="ValidationHelper"/>.</param>
        public ValidationHelper(ValidationSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));

            ValidationRules = new ValidationRuleCollection();
            Settings = settings;
        }

        #endregion

        #region Properties

        private ValidationRuleCollection ValidationRules { get; }
        private ValidationSettings Settings { get; }

        /// <summary>
        /// Indicates whether the validation is currently suspended using the <see cref="SuppressValidation"/> method.
        /// </summary>
        public bool IsValidationSuspended
        {
            get
            {
                return suppressToggle.Value;
            }
        }

        #endregion

        #region Rules Construction

        /// <summary>
        /// Adds a validation rule that validates the <paramref name="target"/> object.
        /// </summary>
        /// <param name="target">The validation target (object that is being validated by <paramref name="validateDelegate"/>).</param>
        /// <param name="validateDelegate">
        /// The validation delegate - a function that returns an instance 
        /// of <see cref="RuleResult"/> that indicated whether the rule has passed and 
        /// a collection of errors (in not passed).
        /// </param>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IValidationRule AddRule([NotNull] object target, [NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(target, nameof(target));
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            var rule = AddRuleCore(new GenericValidationTarget(target), validateDelegate, null);

            return rule;
        }

        /// <summary>
        /// Adds a simple validation rule.
        /// </summary>
        /// <param name="validateDelegate">
        /// The validation delegate - a function that returns an instance 
        /// of <see cref="RuleResult"/> that indicated whether the rule has passed and 
        /// a collection of errors (in not passed).
        /// </param>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IValidationRule AddRule([NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            var rule = AddRuleCore(new UndefinedValidationTarget(), validateDelegate, null);

            return rule;
        }

        /// <summary>
        /// Adds a validation rule that validates a property of an object. The target property is specified in the <paramref name="targetName"/> parameter.
        /// </summary>
        /// <param name="targetName">The target property name. Example: AddRule(nameof(MyProperty), ...).</param>
        /// <param name="validateDelegate">
        /// The validation delegate - a function that returns an instance 
        /// of <see cref="RuleResult"/> that indicated whether the rule has passed and 
        /// a collection of errors (in not passed).
        /// </param>
        /// <example>
        /// <code>
        /// AddRule(() => Foo, , () => RuleResult.Assert(Foo > 10, "Foo must be greater than 10"))
        /// </code>
        /// </example>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IValidationRule AddRule([NotNull] string targetName, [NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(targetName, nameof(targetName));
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            var rule = AddRule(new[] { targetName }, validateDelegate);

            return rule;
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a <see cref="string"/> argument (<see cref="AddRule(string,System.Func{MvvmValidation.RuleResult})"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        public IValidationRule AddRule([NotNull] Expression<Func<object>> propertyExpression,
            [NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(propertyExpression, nameof(propertyExpression));
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            return AddRule(PropertyName.For(propertyExpression, false), validateDelegate);
        }

        /// <summary>
        /// Adds a validation rule that validates two dependent properties.
        /// </summary>
        /// <param name="property1Name">The first target property name. Example: AddRule(nameof(MyProperty), ...).</param>
        /// <param name="property2Name">The second target property name. Example: AddRule(..., nameof(MyProperty), ...).</param>
        /// <param name="validateDelegate">
        /// The validation delegate - a function that returns an instance 
        /// of <see cref="RuleResult"/> that indicated whether the rule has passed and 
        /// a collection of errors (in not passed).
        /// </param>
        /// <example>
        /// <code>
        /// AddRule(() => Foo, () => Bar, () => RuleResult.Assert(Foo > Bar, "Foo must be greater than bar"))
        /// </code>
        /// </example>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IValidationRule AddRule([NotNull] string property1Name, [NotNull] string property2Name,
            [NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(property1Name, nameof(property1Name));
            Guard.NotNull(property2Name, nameof(property2Name));
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            var rule = AddRule(new[] { property1Name, property2Name }, validateDelegate);

            return rule;
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a <see cref="string"/> arguments (<see cref="AddRule(string, string,Func{RuleResult})"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public IValidationRule AddRule([NotNull] Expression<Func<object>> property1Expression,
            [NotNull] Expression<Func<object>> property2Expression, [NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(property1Expression, nameof(property1Expression));
            Guard.NotNull(property2Expression, nameof(property2Expression));
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            return AddRule(PropertyName.For(property1Expression, false), PropertyName.For(property2Expression, false), validateDelegate);
        }

        /// <summary>
        /// Adds a validation rule that validates a collection of dependent properties.
        /// </summary>
        /// <param name="properties">The collection of target property expressions. Example: AddRule(new [] { () => MyProperty1, () => MyProperty2, () => MyProperty3 }, ...).</param>
        /// <param name="validateDelegate">
        /// The validation delegate - a function that returns an instance 
        /// of <see cref="RuleResult"/> that indicated whether the rule has passed and 
        /// a collection of errors (in not passed).
        /// </param>
        /// <returns>An instance of <see cref="IValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IValidationRule AddRule([NotNull] IEnumerable<string> properties,
            [NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(properties, nameof(properties));
            Guard.Assert(properties.Any(), "properties.Any()");
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            var target = CreatePropertyValidationTarget(properties);

            var rule = AddRuleCore(target, validateDelegate, null);

            return rule;
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a collection of <see cref="string"/> (<see cref="AddRule(IEnumerable{string},Func{RuleResult})"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public IValidationRule AddRule([NotNull] IEnumerable<Expression<Func<object>>> properties,
            [NotNull] Func<RuleResult> validateDelegate)
        {
            Guard.NotNull(properties, nameof(properties));
            Guard.NotNull(validateDelegate, nameof(validateDelegate));

            return AddRule(properties.Select(x => PropertyName.For(x, false)), validateDelegate);
        }

        #region Async Rules

        /// <summary>
        /// Adds an asynchronious validation rule that validates the <paramref name="target"/> object.
        /// </summary>
        /// <param name="target">The validation target (object that is being validated by <paramref name="validateAction"/>).</param>
        /// <param name="validateAction">The validation delegate - a function that performs asyncrhonious validation.</param>
        /// <returns>An instance of <see cref="IAsyncValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] object target,
            [NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(target, nameof(target));
            Guard.NotNull(validateAction, nameof(validateAction));

            var rule = AddRuleCore(new GenericValidationTarget(target), null, validateAction);

            return rule;
        }

        /// <summary>
        /// Adds an asynchronious validation rule.
        /// </summary>
        /// <param name="validateAction">The validation delegate - a function that performs asyncrhonious validation.</param>
        /// <returns>An instance of <see cref="IAsyncValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(validateAction, nameof(validateAction));

            var rule = AddRuleCore(new UndefinedValidationTarget(), null, validateAction);

            return rule;
        }

        /// <summary>
        /// Adds an asynchronious validation rule that validates a property of an object. The target property is specified in the <paramref name="propertyName"/> parameter.
        /// </summary>
        /// <param name="propertyName">The target property name. Example: AddAsyncRule(nameof(MyProperty), ...).</param>
        /// <param name="validateAction">The validation delegate - a function that performs asyncrhonious validation.</param>
        /// <example>
        /// <code>
        /// AddRule(() => Foo, 
        ///         () => 
        ///         {
        ///             return ValidationServiceFacade.ValidateFooAsync(Foo)
        ///                 .ContinueWith(t => return RuleResult.Assert(t.Result.IsValid, "Foo must be greater than 10"));
        ///         })
        /// </code>
        /// </example>
        /// <returns>An instance of <see cref="IAsyncValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] string propertyName,
            [NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(propertyName, nameof(propertyName));
            Guard.NotNull(validateAction, nameof(validateAction));

            var rule = AddAsyncRule(new[] { propertyName }.Select(c => c), validateAction);

            return rule;
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a <see cref="string"/> argument (<see cref="AddAsyncRule(string,Func{Task{RuleResult}})"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] Expression<Func<object>> propertyExpression,
            [NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(propertyExpression, nameof(propertyExpression));
            Guard.NotNull(validateAction, nameof(validateAction));

            return AddAsyncRule(PropertyName.For(propertyExpression, false), validateAction);
        }

        /// <summary>
        /// Adds an asynchronious validation rule that validates two dependent properties.
        /// </summary>
        /// <param name="property1Name">The first target property name. Example: AddRule(nameof(MyProperty), ...).</param>
        /// <param name="property2Name">The second target property name. Example: AddRule(..., nameof(MyProperty), ...).</param>
        /// <param name="validateAction">The validation delegate - a function that performs asyncrhonious validation.</param>
        /// <example>
        /// <code>
        /// AddRule(() => Foo, () => Bar
        ///            () => 
        ///         {
        ///                return ValidationServiceFacade.ValidateFooAndBar(Foo, Bar)
        ///                       .ContinueWith(t => RuleResult.Assert(t.Result.IsValid, "Foo must be greater than 10"));
        ///            })
        /// </code>
        /// </example>
        /// <returns>An instance of <see cref="IAsyncValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] string property1Name, [NotNull] string property2Name,
            [NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(property1Name, nameof(property1Name));
            Guard.NotNull(property2Name, nameof(property2Name));
            Guard.NotNull(validateAction, nameof(validateAction));

            var rule = AddAsyncRule(new[] { property1Name, property2Name }, validateAction);

            return rule;
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes <see cref="string"/> arguments (<see cref="AddAsyncRule(string,string,Func{Task{RuleResult}})"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] Expression<Func<object>> property1Expression, [NotNull] Expression<Func<object>> property2Expression, [NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(property1Expression, nameof(property1Expression));
            Guard.NotNull(property2Expression, nameof(property2Expression));
            Guard.NotNull(validateAction, nameof(validateAction));

            return AddAsyncRule(PropertyName.For(property1Expression, false), PropertyName.For(property2Expression, false), validateAction);
        }

        /// <summary>
        /// Adds an asynchronious validation rule that validates a collection of dependent properties.
        /// </summary>
        /// <param name="properties">The collection of target property names. Example: AddAsyncRule(new [] { nameof(MyProperty1), nameof(MyProperty2), nameof(MyProperty3) }, ...).</param>
        /// <param name="validateAction">The validation delegate - a function that performs asyncrhonious validation.</param>
        /// <returns>An instance of <see cref="IAsyncValidationRule"/> that represents the newly created validation rule.</returns>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] IEnumerable<string> properties,
            [NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(properties, nameof(properties));
            Guard.Assert(properties.Any(), "properties.Any()");
            Guard.NotNull(validateAction, nameof(validateAction));

            var target = CreatePropertyValidationTarget(properties);

            var rule = AddRuleCore(target, null, validateAction);

            return rule;
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a collection of <see cref="string"/> (<see cref="AddAsyncRule(IEnumerable{string},Func{Task{RuleResult}})"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public IAsyncValidationRule AddAsyncRule([NotNull] IEnumerable<Expression<Func<object>>> properties, [NotNull] Func<Task<RuleResult>> validateAction)
        {
            Guard.NotNull(properties, nameof(properties));
            Guard.NotNull(validateAction, nameof(validateAction));

            return AddAsyncRule(properties.Select(x => PropertyName.For(x, false)), validateAction);
        }

        #endregion

        /// <summary>
        /// Removes the specified <paramref name="rule"/>.
        /// </summary>
        /// <param name="rule">Validation rule instance.</param>
        public void RemoveRule([NotNull] IValidationRule rule)
        {
            Guard.NotNull(rule, nameof(rule));

            var typedRule = rule as ValidationRule;

            Guard.Assert(typedRule != null,
                string.Format(CultureInfo.InvariantCulture, "Rule must be of type \"{0}\".",
                    typeof(ValidationRule).FullName));

            lock (syncRoot)
            {
                UnregisterValidationRule(typedRule);

                lock (ruleValidationResultMap)
                {
                    // Clear the results if any
                    foreach (var ruleResultsPair in ruleValidationResultMap)
                    {
                        bool removed = ruleResultsPair.Value.Remove(typedRule);

                        if (removed)
                        {
                            // Notify that validation result for the target has changed
                            NotifyResultChanged(ruleResultsPair.Key, GetResult(ruleResultsPair.Key), null, false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes all validation rules.
        /// </summary>
        public void RemoveAllRules()
        {
            lock (syncRoot)
            {
                UnregisterAllValidationRules();

                object[] targets;

                lock (ruleValidationResultMap)
                {
                    targets = ruleValidationResultMap.Keys.ToArray();

                    // Clear the results
                    ruleValidationResultMap.Clear();
                }

                // Notify that validation result has changed
                foreach (object target in targets)
                {
                    NotifyResultChanged(target, ValidationResult.Valid, null, false);
                }
            }
        }

        private IAsyncValidationRule AddRuleCore(IValidationTarget target, Func<RuleResult> validateDelegate,
            Func<Task<RuleResult>> asyncValidateAction)
        {
            var rule = new ValidationRule(target, validateDelegate, asyncValidateAction);

            RegisterValidationRule(rule);

            return rule;
        }

        private static IValidationTarget CreatePropertyValidationTarget(IEnumerable<string> properties)
        {
            IValidationTarget target;

            // Getting rid of thw "Possible multiple enumerations of IEnumerable" warning
            properties = properties.ToArray();

            if (properties.Count() == 1)
            {
                target = new PropertyValidationTarget(properties.First());
            }
            else
            {
                target = new PropertyCollectionValidationTarget(properties);
            }
            return target;
        }

        private void RegisterValidationRule(ValidationRule rule)
        {
            lock (syncRoot)
            {
                ValidationRules.Add(rule);
            }
        }

        private void UnregisterValidationRule(ValidationRule rule)
        {
            lock (syncRoot)
            {
                ValidationRules.Remove(rule);
            }
        }

        private void UnregisterAllValidationRules()
        {
            lock (syncRoot)
            {
                ValidationRules.Clear();
            }
        }

        #endregion

        #region Getting Validation Results

        /// <summary>
        /// Returns the current validation state (all errors tracked by this instance of <see cref="ValidationHelper"/>).
        /// </summary>
        /// <returns>An instance of <see cref="ValidationResult"/> that contains an indication whether the object is valid and a collection of errors if not.</returns>
        [NotNull]
        public ValidationResult GetResult()
        {
            return GetResultInternal();
        }

        /// <summary>
        /// Returns the current validation state for the given <paramref name="target"/> (all errors tracked by this instance of <see cref="ValidationHelper"/>).
        /// </summary>
        /// <param name="target">The validation target for which to retrieve the validation state.</param>
        /// <returns>An instance of <see cref="ValidationResult"/> that contains an indication whether the object is valid and a collection of errors if not.</returns>
        [NotNull]
        public ValidationResult GetResult([NotNull] object target)
        {
            Guard.NotNull(target, nameof(target));

            bool returnAllResults = string.IsNullOrEmpty(target as string);

            var result = returnAllResults ? GetResultInternal() : GetResultInternal(target);

            return result;
        }

        /// <summary>
        /// Returns the current validation state for a property represented by <paramref name="targetName"/> (all errors tracked by this instance of <see cref="ValidationHelper"/>).
        /// </summary>
        /// <param name="targetName">The property for which to retrieve the validation state. Example: GetResult(() => MyProperty)</param>
        /// <returns>An instance of <see cref="ValidationResult"/> that contains an indication whether the object is valid and a collection of errors if not.</returns>
        [NotNull]
        public ValidationResult GetResult([NotNull] string targetName)
        {
            Guard.NotNull(targetName, nameof(targetName));

            return GetResult((object) targetName);
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a <see cref="string"/> argument (<see cref="GetResult(string)"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public ValidationResult GetResult([NotNull] Expression<Func<object>> propertyExpression)
        {
            Guard.NotNull(propertyExpression, nameof(propertyExpression));

            return GetResult(PropertyName.For(propertyExpression, false));
        }

        private ValidationResult GetResultInternal(object target)
        {
            var result = ValidationResult.Valid;

            lock (ruleValidationResultMap)
            {

                if (ruleValidationResultMap.TryGetValue(target, out var ruleResultMap))
                {
                    foreach (var ruleValidationResult in ruleResultMap.Values)
                    {
                        result = result.Combine(new ValidationResult(target, ruleValidationResult));
                    }
                }
            }

            return result;
        }

        private ValidationResult GetResultInternal()
        {
            var result = ValidationResult.Valid;

            lock (ruleValidationResultMap)
            {
                foreach (var ruleResultsMapPair in ruleValidationResultMap)
                {
                    object ruleTarget = ruleResultsMapPair.Key;
                    var ruleResultsMap = ruleResultsMapPair.Value;

                    foreach (var validationResult in ruleResultsMap.Values)
                    {
                        result = result.Combine(new ValidationResult(ruleTarget, validationResult));
                    }
                }
            }

            return result;
        }

        #endregion

        #region Validation Execution

        /// <summary>
        /// Validates (executes validation rules) the property specified in the <paramref name="targetName"/> parameter.
        /// </summary>
        /// <param name="targetName">Name of the property to validate. Example: Validate(nameof(MyProperty)).</param>
        /// <returns>Result that indicates whether the given property is valid and a collection of errors, if not valid.</returns>
        [NotNull]
        public ValidationResult Validate([NotNull] string targetName)
        {
            Guard.NotNull(targetName, nameof(targetName));

            return ValidateInternal(targetName);
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a <see cref="string"/> argument (<see cref="Validate(string)"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public ValidationResult Validate([NotNull] Expression<Func<object>> propertyPathExpression)
        {
            Guard.NotNull(propertyPathExpression, nameof(propertyPathExpression));

            return Validate(PropertyName.For(propertyPathExpression, false));
        }

        /// <summary>
        /// Validates (executes validation rules) the specified target object.
        /// </summary>
        /// <param name="target">The target object to validate.</param>
        /// <returns>Result that indicates whether the given target object is valid and a collection of errors, if not valid.</returns>
        [NotNull]
        public ValidationResult Validate([NotNull] object target)
        {
            Guard.NotNull(target, nameof(target));

            return ValidateInternal(target);
        }

        /// <summary>
        /// Validates (executes validation rules) the calling property.
        /// </summary>
        /// <param name="callerName">Name of the property to validate (provided by the c# compiler and should not be specified exlicitly).</param>
        /// <returns>Result that indicates whether the given property is valid and a collection of errors, if not valid.</returns>
        [NotNull]
        public ValidationResult ValidateCaller([CallerMemberName] string callerName = null)
        {
            Guard.NotNullOrEmpty(callerName, nameof(callerName));

            return Validate(callerName);
        }

        /// <summary>
        /// Executes validation using all validation rules. 
        /// </summary>
        /// <returns>Result that indicates whether the validation was succesfull and a collection of errors, if it wasn't.</returns>
        [NotNull]
        public ValidationResult ValidateAll()
        {
            return ValidateInternal(null);
        }

        private ValidationResult ValidateInternal(object target)
        {
            ReadOnlyCollection<ValidationRule> rulesToExecute;

            lock (syncRoot)
            {
                if (IsValidationSuspended)
                {
                    return ValidationResult.Valid;
                }

                rulesToExecute = GetRulesForTarget(target);

                if (rulesToExecute.Any(r => !r.SupportsSyncValidation))
                {
                    throw new InvalidOperationException(
                        "There are asynchronous rules that cannot be executed synchronously. Please use ValidateAsync method to execute validation instead.");
                }
            }

            try
            {
                var validationResult = ExecuteValidationRulesAsync(rulesToExecute).Result;
                return validationResult;
            }
            catch (Exception ex)
            {
                throw new ValidationException(
                    "An exception occurred during validation. See inner exception for details.",
                    ExceptionUtils.UnwrapException(ex));
            }
        }

        private async Task<ValidationResult> ExecuteValidationRulesAsync(IEnumerable<ValidationRule> rulesToExecute, SynchronizationContext syncContext = null)
        {
            var result = new ValidationResult();
            var failedTargets = new HashSet<object>();

            foreach (var rule in rulesToExecute.ToArray())
            {
                bool isValid = await ExecuteRuleAsync(rule, failedTargets, result, syncContext).ConfigureAwait(false);

                if (!isValid)
                {
                    break;
                }
            }

            return result;
        }

        private async Task<bool> ExecuteRuleAsync(ValidationRule rule, ISet<object> failedTargets, ValidationResult validationResultAccumulator, SynchronizationContext syncContext)
        {
            // Skip rule if the target is already invalid and the rule is not configured to execute anyway
            if (failedTargets.Contains(rule.Target) && !ShouldExecuteOnAlreadyInvalidTarget(rule))
            {
                // Assume that the rule is valid at this point because we are not interested in this error until
                // previous rule is fixed.
                SaveRuleValidationResultAndNotifyIfNeeded(rule, RuleResult.Valid(), syncContext);

                return true;
            }

            var ruleResult = !rule.SupportsSyncValidation
                ? await rule.EvaluateAsync().ConfigureAwait(false)
                : rule.Evaluate();

            SaveRuleValidationResultAndNotifyIfNeeded(rule, ruleResult, syncContext);

            AddErrorsFromRuleResult(validationResultAccumulator, rule, ruleResult);

            if (!ruleResult.IsValid)
            {
                failedTargets.Add(rule.Target);
            }

            return true;
        }

        private bool ShouldExecuteOnAlreadyInvalidTarget(ValidationRule rule)
        {
            bool defaultValue = Settings.DefaultRuleSettings?.ExecuteOnAlreadyInvalidTarget ?? false;

            return rule.Settings.ExecuteOnAlreadyInvalidTarget.GetValueOrDefault(defaultValue);
        }

        private ReadOnlyCollection<ValidationRule> GetRulesForTarget(object target)
        {
            lock (syncRoot)
            {
                var ruleFilter = CreateRuleFilterFor(target);

                var result = new ReadOnlyCollection<ValidationRule>(ValidationRules.Where(ruleFilter).ToList());

                return result;
            }
        }

        private static void AddErrorsFromRuleResult(ValidationResult resultToAddTo, ValidationRule validationRule,
            RuleResult ruleResult)
        {
            if (!ruleResult.IsValid)
            {
                var errorTargets = validationRule.Target.UnwrapTargets();

                foreach (object errorTarget in errorTargets)
                {
                    foreach (string ruleError in ruleResult.Errors)
                    {
                        resultToAddTo.AddError(errorTarget, ruleError, ruleResult);
                    }
                }
            }
        }

        private static Func<ValidationRule, bool> CreateRuleFilterFor(object target)
        {
            Func<ValidationRule, bool> ruleFilter = r => true;

            if (target != null)
            {
                ruleFilter = r => r.Target.IsMatch(target);
            }

            return ruleFilter;
        }

        private void SaveRuleValidationResultAndNotifyIfNeeded(ValidationRule rule, RuleResult ruleResult,
            SynchronizationContext syncContext)
        {
            lock (syncRoot)
            {
                var ruleTargets = rule.Target.UnwrapTargets();

                foreach (object ruleTarget in ruleTargets)
                {
                    var targetRuleMap = GetRuleMapForTarget(ruleTarget);

                    var currentRuleResult = GetCurrentValidationResultForRule(targetRuleMap, rule);

                    if (!Equals(currentRuleResult, ruleResult))
                    {
                        lock (ruleValidationResultMap)
                        {
                            targetRuleMap[rule] = ruleResult;
                        }

                        // Notify that validation result for the target has changed
                        NotifyResultChanged(ruleTarget, GetResult(ruleTarget), syncContext);
                    }
                }
            }
        }

        private static RuleResult GetCurrentValidationResultForRule(
            IDictionary<ValidationRule, RuleResult> ruleMap, ValidationRule rule)
        {
            lock (ruleMap)
            {
                if (!ruleMap.ContainsKey(rule))
                {
                    ruleMap.Add(rule, RuleResult.Valid());
                }

                return ruleMap[rule];
            }
        }

        private IDictionary<ValidationRule, RuleResult> GetRuleMapForTarget(object target)
        {
            lock (ruleValidationResultMap)
            {
                if (!ruleValidationResultMap.ContainsKey(target))
                {
                    ruleValidationResultMap.Add(target, new Dictionary<ValidationRule, RuleResult>());
                }

                var ruleMap = ruleValidationResultMap[target];

                return ruleMap;
            }
        }

        /// <summary>
        /// Executes validation for the given property asynchronously. 
        /// Executes all (normal and async) validation rules for the property specified in the <paramref name="targetName"/>.
        /// </summary>
        /// <param name="targetName">Expression for the property to validate. Example: ValidateAsync(() => MyProperty, ...).</param>
        /// <returns>Task that represents the validation operation.</returns>
        [NotNull]
        public Task<ValidationResult> ValidateAsync([NotNull] string targetName)
        {
            Guard.NotNull(targetName, nameof(targetName));

            return ValidateInternalAsync(targetName);
        }

        /// <summary>
        /// OBSOLTE: If you are using C# 6 compiler consider using another overload of this method that 
        /// takes a <see cref="string"/> argument (<see cref="ValidateAsync(string)"/>) 
        /// and invoke it with nameof(MyProperty) instead. 
        /// </summary>
        [NotNull]
        public Task<ValidationResult> ValidateAsync([NotNull] Expression<Func<object>> propertyPathExpression)
        {
            Guard.NotNull(propertyPathExpression, nameof(propertyPathExpression));

            return ValidateAsync(PropertyName.For(propertyPathExpression, false));
        }

        /// <summary>
        /// Executes validation for the calling property asynchronously.
        /// </summary>
        /// <param name="callerName">Name of the property to validate (provided by the c# compiler and should not be specified exlicitly).</param>
        /// <returns>Result that indicates whether the given property is valid and a collection of errors, if not valid.</returns>
        [NotNull]
        public Task<ValidationResult> ValidateCallerAsync([CallerMemberName] string callerName = null)
        {
            Guard.NotNullOrEmpty(callerName, nameof(callerName));

            return ValidateAsync(callerName);
        }

        /// <summary>
        /// Executes validation for the given target asynchronously. 
        /// Executes all (normal and async) validation rules for the target object specified in the <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The target object to validate.</param>
        /// <returns>Task that represents the validation operation.</returns>
        [NotNull]
        public Task<ValidationResult> ValidateAsync([NotNull] object target)
        {
            Guard.NotNull(target, nameof(target));

            return ValidateInternalAsync(target);
        }

        /// <summary>
        /// Executes validation using all validation rules asynchronously.
        /// </summary>
        /// <returns>Task that represents the validation operation.</returns>
        [NotNull]
        public Task<ValidationResult> ValidateAllAsync()
        {
            return ValidateInternalAsync(null);
        }

        private Task<ValidationResult> ValidateInternalAsync(object target)
        {
            if (IsValidationSuspended)
            {
                return TaskEx.FromResult(ValidationResult.Valid);
            }

            var rulesToExecute = GetRulesForTarget(target);

            var syncContext = SynchronizationContext.Current;

            return Task.Factory.StartNew(() => ExecuteValidationRulesAsync(rulesToExecute, syncContext),
                    CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default)
                .Unwrap()
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        throw new ValidationException(
                            "An exception occurred during validation. See inner exception for details.",
                            ExceptionUtils.UnwrapException(t.Exception));
                    }

                    return t.Result;
                });
        }

        #endregion

        #region ResultChanged

        /// <summary>
        /// Occurs when the validation result have changed for a property or for the entire entity (the result that is returned by the <see cref="GetResult()"/> method).
        /// </summary>
        public event EventHandler<ValidationResultChangedEventArgs> ResultChanged;

        private void NotifyResultChanged(object target, ValidationResult newResult, SynchronizationContext syncContext,
            bool useSyncContext = true)
        {
            if (useSyncContext && syncContext != null)
            {
                syncContext.Post(_ => NotifyResultChanged(target, newResult, syncContext, useSyncContext: false), null);
                return;
            }

            ResultChanged?.Invoke(this, new ValidationResultChangedEventArgs(target, newResult));
        }

        #endregion

        #region Misc

        /// <summary>
        /// Suppresses all the calls to the Validate* methods until the returned <see cref="IDisposable"/> is disposed
        /// by calling <see cref="IDisposable.Dispose"/>. 
        /// </summary>
        /// <remarks>
        /// This method is convenient to use when you want to suppress validation when setting initial value to a property. In this case you would
        /// wrap the code that sets the property into a <c>using</c> block. Like this:
        /// <code>
        /// using (Validation.SuppressValidation()) 
        /// {
        ///     MyProperty = "Initial Value";
        /// }
        /// </code>
        /// </remarks>
        /// <returns>An instance of <see cref="IDisposable"/> that serves as a handle that you can call <see cref="IDisposable.Dispose"/> on to resume validation. The value can also be used in a <c>using</c> block.</returns>
        [NotNull]
        public IDisposable SuppressValidation()
        {
            return suppressToggle.Toggle();
        }

        /// <summary>
        /// Resets the validation state. If there were any broken rules 
        /// then the targets for those rules will become valid again and the <see cref="ResultChanged"/> event will be rised.
        /// </summary>
        public void Reset()
        {
            lock (syncRoot)
            {
                lock (ruleValidationResultMap)
                {
                    object[] targets = ruleValidationResultMap.Keys.ToArray();

                    foreach (object target in targets)
                    {
                        var resultForTarget = GetResultInternal(target);

                        ruleValidationResultMap.Remove(target);

                        if (!resultForTarget.IsValid)
                        {
                            NotifyResultChanged(target, ValidationResult.Valid, null, false);
                        }
                    }
                }
            }
        }

        #endregion
    }
}