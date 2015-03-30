﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using Microsoft.Azure.Commands.Sql.Common;
using Microsoft.Azure.Commands.Sql.Server.Model;
using Microsoft.Azure.Common.Authentication.Models;
using Microsoft.Azure.Management.Sql;
using Microsoft.Azure.Management.Sql.Models;

namespace Microsoft.Azure.Commands.Sql.Server.Adapter
{
    /// <summary>
    /// Adapter for server operations
    /// </summary>
    public class AzureSqlDatabaseServerAdapter
    {
        private AzureEndpointsCommunicator AzureCommunicator { get; set; }

        /// <summary>
        /// Gets or sets the Azure profile
        /// </summary>
        public AzureProfile Profile { get; set; }

        /// <summary>
        /// Constructs a server adapter
        /// </summary>
        /// <param name="profile">The current azure profile</param>
        /// <param name="subscription">The current azure subscription</param>
        public AzureSqlDatabaseServerAdapter(AzureProfile profile, AzureSubscription subscription)
        {
            Profile = profile;
            AzureCommunicator = new AzureEndpointsCommunicator(Profile, subscription);
        }

        /// <summary>
        /// Gets a server in a resource group
        /// </summary>
        /// <param name="resourceGroupName">The name of the resource group</param>
        /// <param name="serverName">The name of the server</param>
        /// <returns>The server</returns>
        public AzureSqlDatabaseServerModel GetServer(string resourceGroupName, string serverName)
        {
            SqlManagementClient client = AzureCommunicator.GetCurrentSqlClient(Guid.NewGuid().ToString());
            
            ServerGetResponse resp = client.Servers.Get(resourceGroupName, serverName);
            return CreateServerModelFromResponse(resourceGroupName, resp.Server);
        }

        /// <summary>
        /// Gets a list of all the servers in a resource group
        /// </summary>
        /// <param name="resourceGroupName">The name of the resource group</param>
        /// <returns>A list of all the servers</returns>
        public List<AzureSqlDatabaseServerModel> GetServers(string resourceGroupName)
        {
            SqlManagementClient client = AzureCommunicator.GetCurrentSqlClient(Guid.NewGuid().ToString());

            ServerListResponse resp = client.Servers.List(resourceGroupName);

            return resp.Servers.Select((s) =>
            {
                return CreateServerModelFromResponse(resourceGroupName, s);
            }).ToList();
        }

        /// <summary>
        /// Upserts a server
        /// </summary>
        /// <param name="model">The server to upsert</param>
        /// <returns>The updated server model</returns>
        public AzureSqlDatabaseServerModel UpsertServer(AzureSqlDatabaseServerModel model)
        {
            SqlManagementClient client = AzureCommunicator.GetCurrentSqlClient(Guid.NewGuid().ToString());

            ServerGetResponse response = client.Servers.CreateOrUpdate(model.ResourceGroupName, model.ServerName, new ServerCreateOrUpdateParameters()
                {
                    Location = model.Location,
                    Tags = model.Tags,
                    Properties = new ServerCreateOrUpdateProperties()
                    {
                        AdministratorLogin = model.SqlAdminUserName,
                        AdministratorLoginPassword = Decrypt(model.SqlAdminPassword),
                        Version = model.ServerVersion,
                    }
                });

            return CreateServerModelFromResponse(model.ResourceGroupName, response.Server);
        }

        /// <summary>
        /// Deletes a server
        /// </summary>
        /// <param name="resourceGroupName">The resource group the server is in</param>
        /// <param name="serverName">The name of the server to delete</param>
        public void RemoveServer(string resourceGroupName, string serverName)
        {
            SqlManagementClient client = AzureCommunicator.GetCurrentSqlClient(Guid.NewGuid().ToString());

            client.Servers.Delete(resourceGroupName, serverName);
        }

        /// <summary>
        /// Convert a Management.Sql.Models.Server to AzureSqlDatabaseServerModel
        /// </summary>
        /// <param name="resourceGroupName">The resource group the server is in</param>
        /// <param name="resp">The management client server response to convert</param>
        /// <returns>The converted server model</returns>
        private static AzureSqlDatabaseServerModel CreateServerModelFromResponse(string resourceGroupName, Management.Sql.Models.Server resp)
        {
            AzureSqlDatabaseServerModel server = new AzureSqlDatabaseServerModel();

            server.ResourceGroupName = resourceGroupName;
            server.ServerName = resp.Name;
            server.ServerVersion = resp.Properties.Version;
            server.SqlAdminUserName = resp.Properties.AdministratorLogin;
            server.Location = resp.Location;

            return server;
        }

        /// <summary>
        /// Convert a <see cref="System.Security.SecureString"/> to a plain-text string representation.
        /// This should only be used in a proetected context, and must be done in the same logon and process context
        /// in which the <see cref="System.Security.SecureString"/> was constructed.
        /// </summary>
        /// <param name="secureString">The encrypted <see cref="System.Security.SecureString"/>.</param>
        /// <returns>The plain-text string representation.</returns>
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private static string Decrypt(SecureString secureString)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}
