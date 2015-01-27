﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using GSoft.Dynamite.Binding;
using GSoft.Dynamite.Fields;
using GSoft.Dynamite.Globalization;
using GSoft.Dynamite.Globalization.Variations;
using GSoft.Dynamite.Logging;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing;

namespace GSoft.Dynamite.ContentTypes
{
    /// <summary>
    /// Helper class for managing content types.
    /// </summary>
    public class ContentTypeHelper : IContentTypeHelper
    {
        private readonly IVariationHelper variationHelper;
        private readonly IResourceLocator resourceLocator;
        private readonly ILogger log;

        /// <summary>
        /// Initializes a new <see cref="ContentTypeHelper"/> instance
        /// </summary>
        /// <param name="variationHelper">Variations helper</param>
        /// <param name="resourceLocator">The resource locator</param>
        /// <param name="log">Logging utility</param>
        public ContentTypeHelper(IVariationHelper variationHelper, IResourceLocator resourceLocator, ILogger log)
        {
            this.variationHelper = variationHelper;
            this.resourceLocator = resourceLocator;
            this.log = log;
        }

        /// <summary>
        /// Ensure the content type based on its content type info. 
        /// Sets the description and Groups resource, adds the fields and calls update.
        /// </summary>
        /// <param name="contentTypeCollection">The content type collection.</param>
        /// <param name="contentTypeInfo">The content type information.</param>
        /// <returns>
        /// The created and configured content type.
        /// </returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public SPContentType EnsureContentType(SPContentTypeCollection contentTypeCollection, ContentTypeInfo contentTypeInfo)
        {
            var contentType = this.EnsureContentType(
                contentTypeCollection,
                new SPContentTypeId(contentTypeInfo.ContentTypeId),
                contentTypeInfo.DisplayNameResourceKey,
                contentTypeInfo.ResourceFileName);

            this.EnsureFieldInContentType(contentType, contentTypeInfo.Fields);

            // Get a list of the available languages and end with the main language
            var web = contentType.ParentWeb;
            var availableLanguages = web.SupportedUICultures.Reverse().ToList();

            // If it's a publishing web, add the variation labels as available languages
            if (PublishingWeb.IsPublishingWeb(web) && this.variationHelper.IsVariationsEnabled(web.Site))
            {
                var labels = this.variationHelper.GetVariationLabels(web.Site);
                if (labels.Count > 0)
                {
                   // Predicate to check if the web contains the label language in it's available languages
                   Func<VariationLabel, bool> notAvailableWebLanguageFunc = (label) => 
                       !availableLanguages.Any(lang => lang.Name.Equals(label.Language, StringComparison.InvariantCultureIgnoreCase));

                    // Get the label languages that aren't already in the web's available languages
                    var labelLanguages = labels
                        .Where(notAvailableWebLanguageFunc)
                        .Select(label => new CultureInfo(label.Language));

                    availableLanguages.AddRange(labelLanguages);
                }
            }

            // If multiple languages are enabled, since we have a full ContentTypeInfo object, we want to populate 
            // all alternate language labels for the Content Type
            foreach (var availableLanguage in availableLanguages)
            {
                // Make sure the ResourceLocator will fetch the correct culture's DisplayName values
                // by forwarding the CultureInfo.
                contentType.Name = this.resourceLocator.Find(contentTypeInfo.ResourceFileName, contentTypeInfo.DisplayNameResourceKey, availableLanguage);
                contentType.Description = this.resourceLocator.Find(contentTypeInfo.ResourceFileName, contentTypeInfo.DescriptionResourceKey, availableLanguage);
                contentType.Group = this.resourceLocator.Find(contentTypeInfo.ResourceFileName, contentTypeInfo.GroupResourceKey, availableLanguage);
            }

            contentType.Update();

            return contentType;
        }

        /// <summary>
        /// Ensure a list of content type
        /// </summary>
        /// <param name="contentTypeCollection">The content type collection</param>
        /// <param name="contentTypeInfos">The content types information</param>
        /// <returns>The content types list</returns>
        public IEnumerable<SPContentType> EnsureContentType(SPContentTypeCollection contentTypeCollection, ICollection<ContentTypeInfo> contentTypeInfos)
        {
            var contentTypes = new List<SPContentType>();

            foreach (ContentTypeInfo contentType in contentTypeInfos)
            {
                contentTypes.Add(this.EnsureContentType(contentTypeCollection, contentType));
            }

            return contentTypes;
        }
        
        /// <summary>
        /// Ensures the SPContentType is in the collection. If not, it will be created and added.
        /// </summary>
        /// <param name="contentTypeCollection">The content type collection.</param>
        /// <param name="contentTypeId">The content type id.</param>
        /// <param name="contentTypeName">Name of the content type. If this is a resource key, the actual resource value will be found (among all default resource file names) and applied.</param>
        /// <returns>
        ///   The content type that was created.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">For any null parameter.</exception>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public SPContentType EnsureContentType(SPContentTypeCollection contentTypeCollection, SPContentTypeId contentTypeId, string contentTypeName)
        {
            return this.EnsureContentType(contentTypeCollection, contentTypeId, contentTypeName, string.Empty);
        }

        /// <summary>
        /// Ensures the SPContentType is in the collection. If not, it will be created and added.
        /// </summary>
        /// <param name="contentTypeCollection">The content type collection.</param>
        /// <param name="contentTypeId">The content type id.</param>
        /// <param name="contentTypeName">Name of the content type. If this is a resource key, the actual resource value will be found and applied.</param>
        /// <param name="resourceFileName">Name of the resource file where the name resource key is located. Is string is empty, will check all default resource file names.</param>
        /// <returns>
        ///   The content type that was created.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">For any null parameter.</exception>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public SPContentType EnsureContentType(SPContentTypeCollection contentTypeCollection, SPContentTypeId contentTypeId, string contentTypeName, string resourceFileName)
        {
            if (contentTypeCollection == null)
            {
                throw new ArgumentNullException("contentTypeCollection");
            }

            if (contentTypeId == null)
            {
                throw new ArgumentNullException("contentTypeId");
            }

            if (string.IsNullOrEmpty(contentTypeName))
            {
                throw new ArgumentNullException("contentTypeName");
            }

            SPList list = null;

            var contentTypeResouceTitle = this.resourceLocator.GetResourceString(resourceFileName, contentTypeName);

            if (TryGetListFromContentTypeCollection(contentTypeCollection, out list))
            {
                // Make sure its not already in the list.
                var contentTypeInList = list.ContentTypes.Cast<SPContentType>().FirstOrDefault(ct => ct.Id == contentTypeId || ct.Parent.Id == contentTypeId);
                if (contentTypeInList == null)
                {
                    // Can we add the content type to the list?
                    if (list.IsContentTypeAllowed(contentTypeId))
                    {
                        // Enable content types if not yet done.
                        if (!list.ContentTypesEnabled)
                        {
                            list.ContentTypesEnabled = true;
                            list.Update(true);
                        }

                        // Try to use the list's web's content type if it already exists
                        var contentTypeInWeb = list.ParentWeb.Site.RootWeb.AvailableContentTypes[contentTypeId];

                        if (contentTypeInWeb == null)
                        {
                            // By convention, content types should always exist on root web as site-collection-wide
                            // content types before they get linked on a specific list.
                            var rootWebContentTypeCollection = list.ParentWeb.Site.RootWeb.ContentTypes;
                            var newWebContentType = new SPContentType(contentTypeId, rootWebContentTypeCollection, contentTypeResouceTitle);
                            contentTypeInWeb = rootWebContentTypeCollection.Add(newWebContentType);

                            this.log.Warn(
                                "EnsureContentType - Forced the creation of Content Type (name={0} ctid={1}) on the root web (url=) instead of adding the CT directly on the list (id={2} title={3}). By convention, all CTs should be provisonned on RootWeb before being re-used in lists.",
                                contentTypeInWeb.Name,
                                contentTypeInWeb.Id.ToString(),
                                list.ID,
                                list.Title);
                        }
                            
                        // Add the web content type to the collection.
                        return list.ContentTypes.Add(contentTypeInWeb);
                    }
                }
                else
                {
                    return contentTypeInList;
                }
            }
            else
            {
                SPWeb web = null;
                if (TryGetWebFromContentTypeCollection(contentTypeCollection, out web))
                {
                    // Make sure its not already in ther web.
                    var contentTypeInWeb = web.ContentTypes[contentTypeId];
                    if (contentTypeInWeb == null)
                    {
                        // Add the content type to the collection.
                        var newWebContentType = new SPContentType(contentTypeId, contentTypeCollection, contentTypeResouceTitle);
                        var returnedWebContentType = contentTypeCollection.Add(newWebContentType);
                        return returnedWebContentType;
                    }
                    else
                    {
                        return contentTypeInWeb;
                    }
                }

                // Case if there is no Content Types in the Web (e.g single SPWeb)
                var newContentType = new SPContentType(contentTypeId, contentTypeCollection, contentTypeResouceTitle);
                var returnedContentType = contentTypeCollection.Add(newContentType);
                return returnedContentType;
            }

            return null;
        }

        /// <summary>
        /// Ensure a single content in a collection
        /// </summary>
        /// <param name="collection">The content type collection</param>
        /// <param name="contentType">The content type info</param>
        /// <returns>The content type object</returns>
        [Obsolete("Prefer ensuring content types with content type info.")]
        public SPContentType EnsureContentType(SPContentTypeCollection collection, SPContentType contentType)
        {
            return this.EnsureContentType(collection, contentType.Id, contentType.Name);
        }

        /// <summary>
        /// Deletes the content type if not used.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="contentTypeId">The content type id.</param>
        /// <exception cref="System.ArgumentNullException">For any null parameter.</exception>
        /// <exception cref="Microsoft.SharePoint.SPContentTypeReadOnlyException">If the contentype is readonly.</exception>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public void DeleteContentTypeIfNotUsed(SPContentTypeCollection collection, SPContentTypeId contentTypeId)
        {
            if (contentTypeId == null)
            {
                throw new ArgumentNullException("contentTypeId");
            }

            if (contentTypeId == null)
            {
                throw new ArgumentNullException("contentTypeId");
            }

            // Get the content type from the web.
            SPContentType contentType = collection[collection.BestMatch(contentTypeId)];

            // return false if the content type does not exist.
            if (contentType != null)
            {
                // Delete the content type if not used.
                this.DeleteContentTypeIfNotUsed(contentType);
            }
        }

        /// <summary>
        /// Deletes the content type if it has no SPContentTypeUsages.
        /// If it does, the content type will be deleted from the usages that are lists where it has no items.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <exception cref="System.ArgumentNullException">For any null parameter.</exception>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public void DeleteContentTypeIfNotUsed(SPContentType contentType)
        {
            // Find where the content type is being used.
            ICollection<SPContentTypeUsage> usages = SPContentTypeUsage.GetUsages(contentType);
            if (usages.Count <= 0)
            {
                // Delete unused content type.
                contentType.ParentWeb.ContentTypes.Delete(contentType.Id);
            }
            else
            {
                // Prepare the query to get all items in a list that uses the content type.
                SPQuery query = new SPQuery()
                {
                    Query = string.Concat(
                            "<Where><Eq>",
                                "<FieldRef Name='ContentTypeId'/>",
                                string.Format(CultureInfo.InvariantCulture, "<Value Type='Text'>{0}</Value>", contentType.Id),
                            "</Eq></Where>")
                };

                // Get the usages that are in a list.
                List<SPContentTypeUsage> listUsages = (from u in usages where u.IsUrlToList select u).ToList();
                foreach (SPContentTypeUsage usage in listUsages)
                {
                    // For a list usage, we get all the items in the list that use the content type.
                    SPList list = contentType.ParentWeb.GetList(usage.Url);
                    SPListItemCollection listItems = list.GetItems(query);

                    // if no items are found...
                    if (listItems.Count <= 0)
                    {
                        // Delete unused content type.
                        list.ContentTypes.Delete(contentType.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Ensures the SPField is in the content type. If not, it will be added and the content type updated.
        /// </summary>
        /// <param name="contentType">Type content type.</param>
        /// <param name="fieldInfo">The field info.</param>
        /// <returns>Null if the field does not exist, else the field is returned.</returns>
        /// <exception cref="System.ArgumentNullException">For any null parameter.</exception>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public SPField EnsureFieldInContentType(SPContentType contentType, IFieldInfo fieldInfo)
        {
            if (contentType == null)
            {
                throw new ArgumentNullException("contentType");
            }

            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            // Get the SPWeb from the contentType
            SPWeb web = contentType.ParentWeb;

            // We get from AvailableFields because we don't need to modify the field.
            SPField field = web.AvailableFields[fieldInfo.Id];

            if (field != null)
            {
                // Add the field to the content type and its children.
                AddFieldToContentType(contentType, field, true, fieldInfo.Required);
            }

            return field;
        }

        /// <summary>
        /// Ensures the SPFields are in the content type. If not, they will be added and the content type updated.
        /// </summary>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="fieldInfos">The field information.</param>
        /// <returns>IEnumerable of SPFields that where found.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public IEnumerable<SPField> EnsureFieldInContentType(SPContentType contentType, ICollection<IFieldInfo> fieldInfos)
        {
            bool fieldWasAdded = false;
            List<SPField> fields = new List<SPField>();

            // For each field we want to add.
            foreach (IFieldInfo fieldInfo in fieldInfos)
            {
                // We get the field from AvailableFields because we don't need to modify the field.
                SPField field = contentType.ParentWeb.AvailableFields[fieldInfo.Id];
                if (field != null)
                {
                    // We add it to the list of fields we got.
                    fields.Add(field);

                    // Then we add it to the content type without updating the content type.
                    if (AddFieldToContentType(contentType, field, false, fieldInfo.Required))
                    {
                        fieldWasAdded = true;
                    }
                }
            }

            if (fieldWasAdded)
            {
                // When One or more fields are added to the content type, we update the content type.
                contentType.Update(true);
            }

            return fields;
        }

        /// <summary>
        /// Adds the event receiver definition to the content type.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <param name="type">The receiver type.</param>
        /// <param name="assemblyName">Name of the assembly.</param>
        /// <param name="className">Name of the class.</param>
        /// <param name="syncType">The synchronization type</param>
        /// <returns>The event receiver definition</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public SPEventReceiverDefinition AddEventReceiverDefinition(SPContentType contentType, SPEventReceiverType type, string assemblyName, string className, SPEventReceiverSynchronization syncType)
        {
            SPEventReceiverDefinition eventReceiverDefinition = null;

            var classType = Type.GetType(string.Format(CultureInfo.InvariantCulture, "{0}, {1}", className, assemblyName));
            if (classType != null)
            {
                var assembly = Assembly.GetAssembly(classType);
                eventReceiverDefinition = this.AddEventReceiverDefinition(contentType, type, assembly, className, syncType);
            }

            return eventReceiverDefinition;
        }

        /// <summary>
        /// Adds the event receiver definition to the content type.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <param name="type">The receiver type.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="className">Name of the class.</param>
        /// <param name="syncType">The synchronization type</param>
        /// <returns>The event receiver definition</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public SPEventReceiverDefinition AddEventReceiverDefinition(SPContentType contentType, SPEventReceiverType type, Assembly assembly, string className, SPEventReceiverSynchronization syncType)
        {
            SPEventReceiverDefinition eventReceiverDefinition = null;

            var isAlreadyDefined = contentType.EventReceivers.Cast<SPEventReceiverDefinition>()
                .Any(x => (x.Class == className) && (x.Type == type));

            // If definition isn't already defined, add it to the content type
            if (!isAlreadyDefined)
            {
                eventReceiverDefinition = contentType.EventReceivers.Add();
                eventReceiverDefinition.Type = type;
                eventReceiverDefinition.Assembly = assembly.FullName;
                eventReceiverDefinition.Synchronization = syncType;
                eventReceiverDefinition.Class = className;
                eventReceiverDefinition.Update();
                contentType.Update(true);
            }

            return eventReceiverDefinition;
        }

        /// <summary>
        /// Remove the event receiver definition for the content type.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <param name="type">The receiver type.</param>
        /// <param name="className">Name of the class.</param>
        public void DeleteEventReceiverDefinition(SPContentType contentType, SPEventReceiverType type, string className)
        {
            var eventReceiverDefinition = contentType.EventReceivers.Cast<SPEventReceiverDefinition>().FirstOrDefault(x => (x.Class == className) && (x.Type == type));

            // If definition isn't already defined, add it to the content type
            if (eventReceiverDefinition != null)
            {
                var eventToDelete = contentType.EventReceivers.Cast<SPEventReceiverDefinition>().Where(eventReceiver => eventReceiver.Type == eventReceiverDefinition.Type).ToList();

                eventToDelete.ForEach(c => c.Delete());
                
                contentType.Update(true);
            }
        }

        /// <summary>
        /// Reorders fields in the content type according to index position.
        /// </summary>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="orderedFields">A collection of indexes (0 based) and their corresponding field information.</param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use of statics is discouraged - this favors more flexibility and consistency with dependency injection.")]
        public void ReorderFieldsInContentType(SPContentType contentType, ICollection<IFieldInfo> orderedFields)
        {
            var fieldInternalNames = contentType.FieldLinks.Cast<SPFieldLink>().Where(x => !x.Hidden).Select(x => x.Name).ToList();

            foreach (var orderedField in orderedFields)
            {
                fieldInternalNames.Remove(orderedField.InternalName);
            }

            var orderedFieldsArray = orderedFields.ToArray();
            for (var i = 0; i < orderedFieldsArray.Length; i++)
            {
                fieldInternalNames.Insert(i, orderedFieldsArray[i].InternalName);
            }

            contentType.FieldLinks.Reorder(fieldInternalNames.ToArray());
            contentType.Update();
        }

        #region Private methods
        private static bool AddFieldToContentType(SPContentType contentType, SPField field, bool updateContentType, RequiredType isRequired)
        {
            // Create the field ref.
            SPFieldLink fieldOneLink = new SPFieldLink(field);
            if (contentType.FieldLinks[fieldOneLink.Id] == null)
            {
                // Set the RequiredType value on the Content Type
                switch (isRequired)
                {
                    case RequiredType.Required:
                        fieldOneLink.Required = true;
                        break;
                    case RequiredType.NotRequired:
                        fieldOneLink.Required = false;
                        break;
                    case RequiredType.Inherit:
                    default:
                        // Do nothing, it will inherit from the Field definition
                        break;
                }

                // Field is not in the content type so we add it.
                contentType.FieldLinks.Add(fieldOneLink);

                // Update the content type.
                if (updateContentType)
                {
                    contentType.Update(true);
                }

                return true;
            }

            return false;
        }

        private static bool TryGetListFromContentTypeCollection(SPContentTypeCollection collection, out SPList list)
        {
            if (collection.Count > 0)
            {
                SPContentType first = collection[0];
                if (first != null)
                {
                    if (first.ParentList != null)
                    {
                        list = first.ParentList;
                        return true;
                    }
                }
            }

            list = null;
            return false;
        }

        private static bool TryGetWebFromContentTypeCollection(SPContentTypeCollection collection, out SPWeb web)
        {
            if (collection.Count > 0)
            {
                SPContentType first = collection[0];
                if (first != null)
                {
                    if (first.ParentWeb != null)
                    {
                        web = first.ParentWeb;
                        return true;
                    }
                }
            }

            web = null;
            return false;
        }
        #endregion
    }
}
