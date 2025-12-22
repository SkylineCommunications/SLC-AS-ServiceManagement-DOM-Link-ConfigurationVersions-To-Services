namespace ServiceManagementDOMLinkConfigurationVersionsToServices
{
	using System;
	using System.Collections.Generic;

	using ServiceManagement_DOM_Link_ConfigurationVersions_To_Services.DomIds;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
	using Skyline.DataMiner.Net.Apps.Sections.Sections;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;
	using Skyline.DataMiner.Utils.DOM.Builders;
	using Skyline.DataMiner.Utils.DOM.Extensions;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public static void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var domHelper = new DomHelper(engine.SendSLNetMessages, SlcServicemanagement.ModuleId);

			var configurationPreviousVersionMap = UpdateServiceConfigurationVersionDomDefinition(domHelper);
			UpdateServiceDomDefinition(domHelper, configurationPreviousVersionMap);
		}

		private static void UpdateServiceDomDefinition(DomHelper domHelper, Dictionary<Guid, Guid?> configurationPreviousVersionMap)
		{
			var serviceInfoSection = domHelper.SectionDefinitions.GetByID(SlcServicemanagement.Sections.ServiceInfo.Id.Id) as CustomSectionDefinition
				?? throw new InvalidOperationException("Service Info section definition not found.");

			var serviceConfigurationField = new DomInstanceFieldDescriptorBuilder()
				.WithID(SlcServicemanagement.Sections.ServiceInfo.ConfigurationVersions)
				.WithName("Configuration Versions")
				.WithTooltip("Reference list to the stored service configuration version in (slc)servicemanagement")
				.WithIsOptional(true)
				.WithType(typeof(List<Guid>))
				.WithModule(SlcServicemanagement.ModuleId)
				.AddDomDefinition(SlcServicemanagement.Definitions.ServiceConfigurationVersion).Build();

			serviceInfoSection.AddOrReplaceFieldDescriptor(serviceConfigurationField);
			domHelper.SectionDefinitions.Update(serviceInfoSection);
			UpdateServiceDomBehaviour(domHelper);

			var servicesPagingHelper = domHelper.DomInstances.PreparePaging(DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagement.Definitions.Services.Id), 100);
			while (servicesPagingHelper.MoveToNextPage())
			{
				List<DomInstance> currentPage = servicesPagingHelper.GetCurrentPage();
				foreach (var domInstance in currentPage)
				{
					UpdateServiceDomInstance(domHelper, configurationPreviousVersionMap, domInstance);
				}
			}
		}

		private static void UpdateServiceDomInstance(DomHelper domHelper, Dictionary<Guid, Guid?> configurationPreviousVersionMap, DomInstance domInstance)
		{
			var currentConfigurationVersion = domInstance.GetFieldValue<Guid>(SlcServicemanagement.Sections.ServiceInfo.Id, SlcServicemanagement.Sections.ServiceInfo.ServiceConfiguration);
			var configurationVersions = new List<Guid>();

			Guid? configVersionToAdd = currentConfigurationVersion?.Value;
			if (configVersionToAdd != null)
			{
				configurationVersions.Add(configVersionToAdd.Value);

				if (configurationPreviousVersionMap.TryGetValue(configVersionToAdd.Value, out var previousVersion) && previousVersion != null)
				{
					configurationVersions.Add(previousVersion.Value);
				}
			}

			domInstance.AddOrUpdateListFieldValue(SlcServicemanagement.Sections.ServiceInfo.Id, SlcServicemanagement.Sections.ServiceInfo.ConfigurationVersions, configurationVersions);
			domHelper.DomInstances.Update(domInstance);
		}

		private static void UpdateServiceDomBehaviour(DomHelper domHelper)
		{
			var serviceBehaviour = domHelper.DomBehaviorDefinitions.GetById(SlcServicemanagement.Behaviors.Service_Behavior.Id.Id);

			foreach (var status in serviceBehaviour.StatusSectionDefinitionLinks)
			{
				status.FieldDescriptorLinks.Add(new DomStatusFieldDescriptorLink(SlcServicemanagement.Sections.ServiceInfo.ConfigurationVersions)
				{ Visible = true, ReadOnly = false, ClientReadOnly = false, RequiredForStatus = false });
			}

			domHelper.DomBehaviorDefinitions.Update(serviceBehaviour);
		}

		private static Dictionary<Guid, Guid?> UpdateServiceConfigurationVersionDomDefinition(DomHelper domHelper)
		{
			var serviceConfigurationInfoSectionDefinition = domHelper.SectionDefinitions.GetByID(SlcServicemanagement.Sections.ServiceConfigurationInfo.Id.Id) as CustomSectionDefinition
				?? throw new InvalidOperationException("Service Configuration Info section definition not found.");
			var serviceConfigurationVersionDomDefinition = domHelper.DomDefinitions.GetByID(SlcServicemanagement.Definitions.ServiceConfigurationVersion.Id)
				?? throw new InvalidOperationException("Service Configuration Version dom definition not found.");

			// Update the DOM definition name to Service Configuration Version
			serviceConfigurationVersionDomDefinition.Name = "Service Configuration Version";

			domHelper.DomDefinitions.Update(serviceConfigurationVersionDomDefinition);

			// Add Create At field to Service Configuration Info section
			var createAtField = new FieldDescriptorBuilder()
				.WithID(SlcServicemanagement.Sections.ServiceConfigurationInfo.CreatedAt)
				.WithName("Create At")
				.WithIsOptional(true)
				.WithType(typeof(DateTime))
				.Build();

			serviceConfigurationInfoSectionDefinition.AddOrReplaceFieldDescriptor(createAtField);
			serviceConfigurationInfoSectionDefinition.Name = "Service Configuration Info";

			Dictionary<Guid, Guid?> configurationPreviousVersionMap = new Dictionary<Guid, Guid?>();

			var configurationsPagingHelper = domHelper.DomInstances.PreparePaging(DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagement.Definitions.ServiceConfigurationVersion.Id), 100);
			var createAtvalue = DateTime.UtcNow;
			while (configurationsPagingHelper.MoveToNextPage())
			{
				List<DomInstance> currentPage = configurationsPagingHelper.GetCurrentPage();
				foreach (var domInstance in currentPage)
				{
					var previousVersion = domInstance.GetFieldValue<Guid>(SlcServicemanagement.Sections.ServiceConfigurationInfo.Id, SlcServicemanagement.Sections.ServiceConfigurationInfo.PreviousVersion);
					configurationPreviousVersionMap[domInstance.ID.Id] = previousVersion?.Value;

					domInstance.RemoveFieldValue(SlcServicemanagement.Sections.ServiceConfigurationInfo.Id, SlcServicemanagement.Sections.ServiceConfigurationInfo.PreviousVersion);
					domInstance.AddOrUpdateFieldValue(SlcServicemanagement.Sections.ServiceConfigurationInfo.Id, SlcServicemanagement.Sections.ServiceConfigurationInfo.CreatedAt, createAtvalue);
					domHelper.DomInstances.Update(domInstance);
				}
			}

			serviceConfigurationInfoSectionDefinition.RemoveFieldDescriptor(SlcServicemanagement.Sections.ServiceConfigurationInfo.PreviousVersion);

			domHelper.SectionDefinitions.Update(serviceConfigurationInfoSectionDefinition);

			return configurationPreviousVersionMap;
		}
	}
}