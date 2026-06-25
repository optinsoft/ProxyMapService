<script setup lang="ts">
import { computed } from 'vue';
import type { HttpImageBodyEntry } from '@/types/log';

const props = defineProps<{
  body: HttpImageBodyEntry;
}>();

const imageUrl = computed(() =>
  `data:${props.body.contentType};base64,${props.body.binaryContentBase64}`
);
</script>

<template>
  <div class="image-viewer">
    <img
      :src="imageUrl"
      alt="response image"
    />
  </div>
</template>

<style scoped>
.image-viewer {
  overflow: auto;
}

.image-viewer img {
  max-width: 100%;
  max-height: 800px;
}
</style>