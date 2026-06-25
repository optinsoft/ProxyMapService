<script setup lang="ts">
import JsonBodyViewer from './JsonBodyViewer.vue';
import XmlBodyViewer from './XmlBodyViewer.vue';
import HtmlBodyViewer from './HtmlBodyViewer.vue';
import TextBodyViewer from './TextBodyViewer.vue';
import ImageBodyViewer from './ImageBodyViewer.vue';
import BinaryBodyViewer from './BinaryBodyViewer.vue';

import { type HttpBodyEntry, HttpContentKind } from '@/types/log';

defineProps<{
  body: HttpBodyEntry;
}>();
</script>

<template>

  <JsonBodyViewer
    v-if="body.contentKind === HttpContentKind.Json"
    :body="body"
  />

  <XmlBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Xml"
    :body="body"
  />

  <HtmlBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Html"
    :body="body"
  />

  <TextBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Text"
    :body="body"
  />

  <ImageBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Image"
    :body="body"
  />

  <BinaryBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Binary"
    :body="body"
  />
</template>