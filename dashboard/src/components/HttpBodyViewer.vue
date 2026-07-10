<script setup lang="ts">
import { computed } from 'vue';
import JsonBodyViewer from './JsonBodyViewer.vue';
import XmlBodyViewer from './XmlBodyViewer.vue';
import HtmlBodyViewer from './HtmlBodyViewer.vue';
import TextBodyViewer from './TextBodyViewer.vue';
import ImageBodyViewer from './ImageBodyViewer.vue';
import BinaryBodyViewer from './BinaryBodyViewer.vue';
import FormUrlEncodedBodyViewer from './FormUrlEncodedBodyViewer.vue';
import MultipartBodyViewer from './MultipartBodyViewer.vue';
import JavascriptBodyViewer from './JavascriptBodyViewer.vue';
import TypescriptBodyViewer from './TypescriptBodyViewer.vue';
import MicrosoftAjaxDeltaBodyViewer from './MicrosoftAjaxDeltaBodyViewer.vue';

import { type HttpBodyEntry, HttpContentKind } from '@/types/http';

const props = defineProps<{
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

  <JavascriptBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Javascript"
    :body="body"
  />

  <TypescriptBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Typescript"
    :body="body"
  />

  <MicrosoftAjaxDeltaBodyViewer
    v-else-if="body.contentKind === HttpContentKind.MicrosoftAjaxDelta"
    :body="body"
  />

  <FormUrlEncodedBodyViewer
    v-else-if="body.contentKind === HttpContentKind.FormUrlEncoded"
    :body="body"
  />

  <MultipartBodyViewer
    v-else-if="body.contentKind === HttpContentKind.MultipartFormData"
    :body="body"
  />

  <ImageBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Image"
    :content-type="props.body.contentType || null"
    :body="body"
  />

  <BinaryBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Binary"
    :body="body"
  />
</template>