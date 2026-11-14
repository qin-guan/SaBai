<script setup lang="ts">
useHead({ title: "Orders · Sa'Bai" })
</script>

<template>
  <div>
    <PageHeader title="Orders" description="Manage orders." :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Orders' }]" />
    <AppPanel title="Orders">
      <StateMessage message="Under construction" />
    </AppPanel>
  </div>
</template>
