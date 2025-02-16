﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;

namespace NZ.XrmToolbox.PersonalArtefactManager.AppCode
{
    internal class OwnerManager
    {
        private readonly PluginContext _pluginContext;
        public Owner[] Owners { get; private set; } = new Owner[] { };
        private Owner[] Teams = new Owner[] { };

        public event EventHandler OwnerListUpdated;

        public OwnerManager(PluginContext context)
        {
            _pluginContext = context;         
        }

        public void LoadSystemOwners()
        {
            _pluginContext.WorkAsync(new WorkAsyncInfo
            {
                Message = "Query list of systemusers",
                Work = (worker, args) =>
                {
                    args.Result = _pluginContext.Service.RetrieveMultiple(new QueryExpression("systemuser")
                    {
                        Orders = { new OrderExpression("fullname", OrderType.Ascending) },
                        PageInfo = { ReturnTotalRecordCount = true },
                        ColumnSet = new ColumnSet("systemuserid", "isdisabled", "internalemailaddress", "fullname"),
                        LinkEntities = { new LinkEntity("systemuser", "systemuserroles", "systemuserid", "systemuserid", JoinOperator.Inner) },
                        Criteria =
                        {
                            Filters =
                            {
                                new FilterExpression
                                {
                                    FilterOperator = LogicalOperator.And,
                                    Conditions =
                                    {
                                        new ConditionExpression("isdisabled", ConditionOperator.Equal, false),
                                        new ConditionExpression("accessmode", ConditionOperator.NotEqual, 3),
                                        new ConditionExpression("accessmode", ConditionOperator.NotEqual, 5),
                                    }
                                }
                            }
                        },
                        Distinct = true
                    });
                },
                PostWorkCallBack = (args) =>
                {
                    // Clear Owner list
                    Owners = new Owner[] { };

                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        var resultSet = args.Result as EntityCollection;
                        if (resultSet != null)
                        {
                            Owners = resultSet.Entities.Select(e => new Owner(e.ToEntityReference(), e.GetAttributeValue<String>("fullname"))).ToArray();
                        }
                    }
                    // There might be a cleaner way to do this
                    // Get the teams and add them to the list
                    _pluginContext.WorkAsync(new WorkAsyncInfo
                    {
                        Message = "Query list of teams",
                        Work = (worker2, args2) =>
                        {
                            args2.Result = _pluginContext.Service.RetrieveMultiple(new QueryExpression("team")
                            {
                                Orders = { new OrderExpression("name", OrderType.Ascending) },
                                PageInfo = { ReturnTotalRecordCount = true },
                                ColumnSet = new ColumnSet("teamid", "teamtype", "name"),
                                Criteria =
                                {
                                    Filters =
                                    {
                                        new FilterExpression
                                        {
                                            FilterOperator = LogicalOperator.And,
                                            Conditions =
                                            {
                                                new ConditionExpression("teamtype", ConditionOperator.Equal, 0)
                                            }
                                        }
                                    }
                                },
                                Distinct = true
                            });
                        },
                        PostWorkCallBack = (args2) =>
                        {
                            if (args2.Error != null)
                            {
                                MessageBox.Show(args2.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {
                                var resultSet = args2.Result as EntityCollection;
                                if (resultSet != null)
                                {
                                    Teams = resultSet.Entities.Select(e => new Owner(e.ToEntityReference(), e.GetAttributeValue<String>("name"))).ToArray();
                                    Owners = Owners.Concat(Teams).ToArray();
                                }
                            }
                            // Notify all subscribers
                            OwnerListUpdated(this, null);
                        }
                    });
                }
            });
        }

        public void Invalidate()
        {
            Owners = new Owner[] {};
            OwnerListUpdated(this, null);
        }
    }
}