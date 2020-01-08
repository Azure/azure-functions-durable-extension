﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function call specifies a parameter that doesn&apos;t match the function definition parameter..
        /// </summary>
        public static string ActivityArgumentAnalyzerDescription {
            get {
                return ResourceManager.GetString("ActivityArgumentAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function named &apos;{0}&apos; takes a &apos;{1}&apos; but was given a &apos;{2}&apos;..
        /// </summary>
        public static string ActivityArgumentAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("ActivityArgumentAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function call is using the wrong argument type..
        /// </summary>
        public static string ActivityArgumentAnalyzerTitle {
            get {
                return ResourceManager.GetString("ActivityArgumentAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function named &apos;{0}&apos; does not exist. Did you mean &apos;{1}&apos;?.
        /// </summary>
        public static string ActivityNameAnalyzerCloseMessageFormat {
            get {
                return ResourceManager.GetString("ActivityNameAnalyzerCloseMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function call references unknown activity function..
        /// </summary>
        public static string ActivityNameAnalyzerDescription {
            get {
                return ResourceManager.GetString("ActivityNameAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function named &apos;{0}&apos; does not exist. Could not find any function references..
        /// </summary>
        public static string ActivityNameAnalyzerMissingMessageFormat {
            get {
                return ResourceManager.GetString("ActivityNameAnalyzerMissingMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function call references unknown Activity function..
        /// </summary>
        public static string ActivityNameAnalyzerTitle {
            get {
                return ResourceManager.GetString("ActivityNameAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function call return type doesn&apos;t match function definition return type..
        /// </summary>
        public static string ActivityReturnTypeAnalyzerDescription {
            get {
                return ResourceManager.GetString("ActivityReturnTypeAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function named &apos;{0}&apos; returns &apos;{1}&apos; but &apos;{2}&apos; is expected..
        /// </summary>
        public static string ActivityReturnTypeAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("ActivityReturnTypeAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Activity function call return type doesn&apos;t match function definition return type..
        /// </summary>
        public static string ActivityReturnTypeAnalyzerTitle {
            get {
                return ResourceManager.GetString("ActivityReturnTypeAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DateTime calls must be deterministic inside an orchestrator function..
        /// </summary>
        public static string DateTimeAnalyzerTitle {
            get {
                return ResourceManager.GetString("DateTimeAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An orchestrator function must be deterministic. For more information on orchestrator code constraints, see:
        ///https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-checkpointing-and-replay#orchestrator-code-constraints.
        /// </summary>
        public static string DeterministicAnalyzerDescription {
            get {
                return ResourceManager.GetString("DeterministicAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; violates the orchestrator deterministic code constraint..
        /// </summary>
        public static string DeterministicAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("DeterministicAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DispatchAsync must be called with the entity class it&apos;s used in..
        /// </summary>
        public static string DispatchClassNameAnalyzerDescription {
            get {
                return ResourceManager.GetString("DispatchClassNameAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DispatchAsync was used with &apos;{0}&apos; but must be called with the entity class &apos;{1}&apos; it&apos;s used in..
        /// </summary>
        public static string DispatchClassNameAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("DispatchClassNameAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DispatchAsync must be called with the entity class it&apos;s used in..
        /// </summary>
        public static string DispatchClassNameAnalyzerTitle {
            get {
                return ResourceManager.GetString("DispatchClassNameAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entity class name and entity function name must match..
        /// </summary>
        public static string EntityClassNameAnalyzerDescription {
            get {
                return ResourceManager.GetString("EntityClassNameAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Class name &apos;{0}&apos; and entity function name &apos;{1}&apos; do not match..
        /// </summary>
        public static string EntityClassNameAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("EntityClassNameAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entity class name and entity function name must match..
        /// </summary>
        public static string EntityClassNameAnalyzerTitle {
            get {
                return ResourceManager.GetString("EntityClassNameAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to EntityTrigger attribute must be used with an IDurableEntityContext..
        /// </summary>
        public static string EntityContextAnalyzerDescription {
            get {
                return ResourceManager.GetString("EntityContextAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to EntityTrigger attribute is applied to a &apos;{0}&apos; but must be used with an IDurableEntityContext instead..
        /// </summary>
        public static string EntityContextAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("EntityContextAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to EntityTrigger attribute must be used with an IDurableEntityContext..
        /// </summary>
        public static string EntityContextAnalyzerTitle {
            get {
                return ResourceManager.GetString("EntityContextAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An entity interface must only contain methods and must contain at least one method..
        /// </summary>
        public static string EntityInterfaceContentAnalyzerDescription {
            get {
                return ResourceManager.GetString("EntityInterfaceContentAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entity interface contains members other than methods:
        /// &apos;{0}&apos;..
        /// </summary>
        public static string EntityInterfaceContentAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("EntityInterfaceContentAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entity interface doesn&apos;t contain any methods and must contain at least one..
        /// </summary>
        public static string EntityInterfaceContentAnalyzerNoMethodsMessageFormat {
            get {
                return ResourceManager.GetString("EntityInterfaceContentAnalyzerNoMethodsMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An entity interface must only contain methods and must contain at least one method..
        /// </summary>
        public static string EntityInterfaceContentAnalyzerTitle {
            get {
                return ResourceManager.GetString("EntityInterfaceContentAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An entity interface must only have methods with at most one parameter..
        /// </summary>
        public static string EntityInterfaceParameterAnalyzerDescription {
            get {
                return ResourceManager.GetString("EntityInterfaceParameterAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method in entity interface has more than one parameter: &apos;{0}&apos;..
        /// </summary>
        public static string EntityInterfaceParameterAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("EntityInterfaceParameterAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An entity interface must only have methods with at most one parameter..
        /// </summary>
        public static string EntityInterfaceParameterAnalyzerTitle {
            get {
                return ResourceManager.GetString("EntityInterfaceParameterAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An entity interface must only have methods that return void, Task, or Task&lt;T&gt;..
        /// </summary>
        public static string EntityInterfaceReturnTypeAnalyzerDescription {
            get {
                return ResourceManager.GetString("EntityInterfaceReturnTypeAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method has a return type &apos;{0}&apos;; return type should only be void, Task, or Task&lt;T&gt;..
        /// </summary>
        public static string EntityInterfaceReturnTypeAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("EntityInterfaceReturnTypeAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An entity interface must only have methods that return void, Task, or Task&lt;T&gt;..
        /// </summary>
        public static string EntityInterfaceReturnTypeAnalyzerTitle {
            get {
                return ResourceManager.GetString("EntityInterfaceReturnTypeAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entity functions must be static..
        /// </summary>
        public static string EntityStaticAnalyzerDescription {
            get {
                return ResourceManager.GetString("EntityStaticAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entity function &apos;{0}&apos; is not marked static..
        /// </summary>
        public static string EntityStaticAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("EntityStaticAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entity functions must be static..
        /// </summary>
        public static string EntityStaticAnalyzerTitle {
            get {
                return ResourceManager.GetString("EntityStaticAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Environment variables must be accessed in a deterministic way inside an orchestrator function..
        /// </summary>
        public static string EnvironmentVariableAnalyzerTitle {
            get {
                return ResourceManager.GetString("EnvironmentVariableAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with (DurableOrchestrationContext).CurrentUtcDateTime.
        /// </summary>
        public static string FixDateTimeInOrchestrator {
            get {
                return ResourceManager.GetString("FixDateTimeInOrchestrator", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Remove Deterministic Attribute.
        /// </summary>
        public static string FixDeterministicAttribute {
            get {
                return ResourceManager.GetString("FixDeterministicAttribute", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with DurableOrchestrationClient.
        /// </summary>
        public static string FixDurableOrchestrationClient {
            get {
                return ResourceManager.GetString("FixDurableOrchestrationClient", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with DurableOrchestrationContext.
        /// </summary>
        public static string FixDurableOrchestrationContext {
            get {
                return ResourceManager.GetString("FixDurableOrchestrationContext", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with DurableOrchestrationContextBase.
        /// </summary>
        public static string FixDurableOrchestrationContextBase {
            get {
                return ResourceManager.GetString("FixDurableOrchestrationContextBase", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace entity function name with class name.
        /// </summary>
        public static string FixEntityFunctionName {
            get {
                return ResourceManager.GetString("FixEntityFunctionName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Add keyword static to the entity function method signature.
        /// </summary>
        public static string FixEntityFunctionStaticModifier {
            get {
                return ResourceManager.GetString("FixEntityFunctionStaticModifier", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with (DurableOrchestrationContext).NewGuid().
        /// </summary>
        public static string FixGuidInOrchestrator {
            get {
                return ResourceManager.GetString("FixGuidInOrchestrator", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with IDurableClient.
        /// </summary>
        public static string FixIDurableClient {
            get {
                return ResourceManager.GetString("FixIDurableClient", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with IDurableEntityClient.
        /// </summary>
        public static string FixIDurableEntityClient {
            get {
                return ResourceManager.GetString("FixIDurableEntityClient", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with IDurableEntityContext.
        /// </summary>
        public static string FixIDurableEntityContext {
            get {
                return ResourceManager.GetString("FixIDurableEntityContext", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with IDurableOrchestrationClient.
        /// </summary>
        public static string FixIDurableOrchestrationClient {
            get {
                return ResourceManager.GetString("FixIDurableOrchestrationClient", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with IDurableOrchestrationContext.
        /// </summary>
        public static string FixIDurableOrchestrationContext {
            get {
                return ResourceManager.GetString("FixIDurableOrchestrationContext", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace with (DurableOrchestrationContext).CreateTimer(DateTime, CancellationToken).
        /// </summary>
        public static string FixTimerInOrchestrator {
            get {
                return ResourceManager.GetString("FixTimerInOrchestrator", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Guid calls must be deterministic inside an orchestrator function..
        /// </summary>
        public static string GuidAnalyzerTitle {
            get {
                return ResourceManager.GetString("GuidAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to I/O operations are not allowed inside an orchestrator function..
        /// </summary>
        public static string IOTypesAnalyzerTitle {
            get {
                return ResourceManager.GetString("IOTypesAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method call &apos;{0}&apos; violates the orchestrator deterministic code constraint. Methods definied in source code that are used in an orchestrator must be deterministic..
        /// </summary>
        public static string MethodAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("MethodAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Methods definied in source code that are used in an orchestrator must be deterministic..
        /// </summary>
        public static string MethodAnalyzerTitle {
            get {
                return ResourceManager.GetString("MethodAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SignalEntityAsync must use an Entity Interface..
        /// </summary>
        public static string SignalEntityAnalyzerDescription {
            get {
                return ResourceManager.GetString("SignalEntityAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An entity interface must be used instead of &apos;{0}&apos;..
        /// </summary>
        public static string SignalEntityAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("SignalEntityAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SignalEntityAsync must use an entity interface..
        /// </summary>
        public static string SignalEntityAnalyzerTitle {
            get {
                return ResourceManager.GetString("SignalEntityAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Thread and Task calls must be deterministic inside an orchestrator function..
        /// </summary>
        public static string ThreadTaskAnalyzerTitle {
            get {
                return ResourceManager.GetString("ThreadTaskAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Thread.Sleep and Task.Delay calls are not allowed inside an orchestrator function..
        /// </summary>
        public static string TimerAnalyzerTitle {
            get {
                return ResourceManager.GetString("TimerAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationClient attribute must be used with a DurableOrchestrationClient..
        /// </summary>
        public static string V1ClientAnalyzerDescription {
            get {
                return ResourceManager.GetString("V1ClientAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationClient attribute is applied to a &apos;{0}&apos; but must be used with a DurableOrchestrationClient..
        /// </summary>
        public static string V1ClientAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("V1ClientAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationClient attribute must be used with a DurableOrchestrationClient..
        /// </summary>
        public static string V1ClientAnalyzerTitle {
            get {
                return ResourceManager.GetString("V1ClientAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationTrigger must be used with an DurableOrchestrationContext or DurableOrchestrationContextBase..
        /// </summary>
        public static string V1OrchestratorContextAnalyzerDescription {
            get {
                return ResourceManager.GetString("V1OrchestratorContextAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationTrigger is applied to a &apos;{0}&apos; but must be used with a DurableOrchestrationContext or a DurableOrchestrationContextBase instead..
        /// </summary>
        public static string V1OrchestratorContextAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("V1OrchestratorContextAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationTrigger must be used with a DurableOrchestrationContext or DurableOrchestrationContextBase..
        /// </summary>
        public static string V1OrchestratorContextAnalyzerTitle {
            get {
                return ResourceManager.GetString("V1OrchestratorContextAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; violates the orchestrator deterministic code constraint. Consider using (DurableOrchestrationContext).CreateTimer&lt;T&gt;(DateTime fireAt, T state, CancellationToken cancelToken).
        /// </summary>
        public static string V1TimerAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("V1TimerAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DurableClient attribute must be used with either an IDurableClient, IDurableEntityClient, or an IDurableOrchestrationClient..
        /// </summary>
        public static string V2ClientAnalyzerDescription {
            get {
                return ResourceManager.GetString("V2ClientAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DurableClient attribute is applied to a &apos;{0}&apos; but must be used with either an IDurableClient, IDurableEntityClient, or an IDurableOrchestrationClient..
        /// </summary>
        public static string V2ClientAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("V2ClientAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DurableClient attribute must be used with either an IDurableClient, IDurableEntityClient, or an IDurableOrchestrationClient..
        /// </summary>
        public static string V2ClientAnalyzerTitle {
            get {
                return ResourceManager.GetString("V2ClientAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationTrigger must be used with an IDurableOrchestrationContext..
        /// </summary>
        public static string V2OrchestratorContextAnalyzerDescription {
            get {
                return ResourceManager.GetString("V2OrchestratorContextAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationTrigger is applied to a &apos;{0}&apos; but must be used with an IDurableOrchestrationContext instead..
        /// </summary>
        public static string V2OrchestratorContextAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("V2OrchestratorContextAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OrchestrationTrigger must be used with an IDurableOrchestrationContext..
        /// </summary>
        public static string V2OrchestratorContextAnalyzerTitle {
            get {
                return ResourceManager.GetString("V2OrchestratorContextAnalyzerTitle", resourceCulture);
            }
        }
    }
}
