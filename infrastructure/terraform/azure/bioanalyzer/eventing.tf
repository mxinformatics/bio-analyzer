resource "azurerm_servicebus_namespace" "bioanalzyer" {
  provider            = azurerm.mxinfo-prod
  name                = join("-", [var.namespace, var.environment, var.location_abbreviation])
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Standard"

  tags = local.tags
}



resource "azurerm_servicebus_namespace_authorization_rule" "publisher" {
  provider     = azurerm.mxinfo-prod
  name         = "bioanalyzer-publish"
  namespace_id = azurerm_servicebus_namespace.bioanalzyer.id

  listen = false
  send   = true
  manage = false
}

resource "azurerm_servicebus_namespace_authorization_rule" "consumer" {
  provider     = azurerm.mxinfo-prod
  name         = "bioanalyzer-consume"
  namespace_id = azurerm_servicebus_namespace.bioanalzyer.id

  listen = true
  send   = false
  manage = false
}

resource "azurerm_servicebus_namespace_authorization_rule" "pub_sub" {
  provider     = azurerm.mxinfo-prod
  name         = "bioanalyzer-pub-sub"
  namespace_id = azurerm_servicebus_namespace.bioanalzyer.id

  listen = true
  send   = true
  manage = false
}


resource "azurerm_servicebus_topic" "bioanalyzer_topic" {
  provider     = azurerm.mxinfo-prod
  for_each     = local.asb_topics
  name         = each.value.name
  namespace_id = azurerm_servicebus_namespace.bioanalzyer.id

  max_size_in_megabytes = 1024
}


locals {
  topic_subscriptions = flatten([
    for topic_key, topic in local.asb_topics : [
      for queue_key, queue in topic.queues : {
        name     = queue.name
        topic_id = azurerm_servicebus_topic.bioanalyzer_topic[topic_key].id
      }
    ]
  ])
}

resource "azurerm_servicebus_queue" "bioanalyzer_queues" {
  for_each     = { for queue in local.topic_subscriptions : queue.name => queue }
  name         = each.value.name
  namespace_id = azurerm_servicebus_namespace.bioanalzyer.id
  provider     = azurerm.mxinfo-prod

  max_size_in_megabytes = 1024

  # Message settings
  max_delivery_count = 2
  lock_duration      = "PT5M"

  # Dead letter settings
  dead_lettering_on_message_expiration = true
}

resource "azurerm_servicebus_subscription" "bioanalyzer_subscriptions" {
  provider           = azurerm.mxinfo-prod
  for_each           = { for queue in local.topic_subscriptions : queue.name => queue }
  name               = "forward-to-${each.value.name}"
  topic_id           = each.value.topic_id
  max_delivery_count = 2
  lock_duration      = "PT5M"
  forward_to         = azurerm_servicebus_queue.bioanalyzer_queues[each.value.name].name
}

resource "azurerm_role_assignment" "asb_manager" {
  provider             = azurerm.mxinfo-prod
  for_each             = local.asb_managers
  scope                = azurerm_servicebus_namespace.bioanalzyer.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = each.value.object_id
}