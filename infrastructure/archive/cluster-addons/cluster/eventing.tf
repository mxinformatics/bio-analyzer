resource "azurerm_servicebus_namespace" "bio_applications" {
  provider            = azurerm.prod-env
  name                = join("-", ["asb", var.namespace, var.environment, var.location_abbreviation])
  location            = azurerm_resource_group.bio_applications.location
  resource_group_name = azurerm_resource_group.bio_applications.name
  sku                 = "Standard"

  tags = local.tags
}

resource "azurerm_servicebus_namespace_authorization_rule" "publisher" {
  provider     = azurerm.prod-env
  name         = "bio-publish"
  namespace_id = azurerm_servicebus_namespace.bio_applications.id

  listen = false
  send   = true
  manage = false
}

resource "azurerm_servicebus_namespace_authorization_rule" "consumer" {
  provider     = azurerm.prod-env
  name         = "bio-consumer"
  namespace_id = azurerm_servicebus_namespace.bio_applications.id

  listen = true
  send   = false
  manage = false
}

resource "azurerm_servicebus_namespace_authorization_rule" "pub_sub" {
  provider     = azurerm.prod-env
  name         = "bio-pub-sub"
  namespace_id = azurerm_servicebus_namespace.bio_applications.id

  listen = true
  send   = true
  manage = false
}

resource "azurerm_servicebus_topic" "bio_applications" {
  provider     = azurerm.prod-env
  for_each     = local.asb_topics
  name         = each.value.name
  namespace_id = azurerm_servicebus_namespace.bio_applications.id
  max_size_in_megabytes = 1024
}


locals {
  topic_subscriptions = flatten([
    for topic_key, topic in local.asb_topics : [
      for queue_key, queue in topic.queues : {
        name     = queue.name
        topic_id = azurerm_servicebus_topic.bio_applications[topic_key].id
      }
    ]
  ])
}

resource "azurerm_servicebus_queue" "bio_applications" {
  provider     = azurerm.prod-env
  for_each     = { for queue in local.topic_subscriptions : queue.name => queue }
  name         = each.value.name
  namespace_id = azurerm_servicebus_namespace.bio_applications.id

  max_size_in_megabytes = 1024

  # Message settings
  max_delivery_count = 10

  # Dead letter settings
  dead_lettering_on_message_expiration = true
}

resource "azurerm_servicebus_subscription" "bio_applications_subscriptions" {
  provider           = azurerm.prod-env
  for_each           = { for queue in local.topic_subscriptions : queue.name => queue }
  name               = "forward-to-${each.value.name}"
  topic_id           = each.value.topic_id
  max_delivery_count = 10
  forward_to         = azurerm_servicebus_queue.bio_applications[each.value.name].name
}

resource "azurerm_key_vault_secret" "bio_asb_consumer_secret" {
  provider     = azurerm.prod-env
  name         = "EventBus--Consumer"
  value        = azurerm_servicebus_namespace_authorization_rule.consumer.primary_connection_string
  key_vault_id = azurerm_key_vault.bio_applications.id
}


resource "azurerm_key_vault_secret" "bio_asb_publisher_secret" {
  provider     = azurerm.prod-env
  name         = "EventBus--Sender"
  value        = azurerm_servicebus_namespace_authorization_rule.publisher.primary_connection_string
  key_vault_id = azurerm_key_vault.bio_applications.id
}