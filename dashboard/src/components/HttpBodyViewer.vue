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
  loadBodyFn: (id: string) => Promise<HttpBodyEntry>;
}>();
</script>

<template>
  <JsonBodyViewer
    v-if="body.contentKind === HttpContentKind.Json"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <XmlBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Xml"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <HtmlBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Html"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <TextBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Text"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <JavascriptBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Javascript"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <TypescriptBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Typescript"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <MicrosoftAjaxDeltaBodyViewer
    v-else-if="body.contentKind === HttpContentKind.MicrosoftAjaxDelta"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <FormUrlEncodedBodyViewer
    v-else-if="body.contentKind === HttpContentKind.FormUrlEncoded"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <MultipartBodyViewer
    v-else-if="body.contentKind === HttpContentKind.MultipartFormData"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <ImageBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Image"
    :content-type="props.body.contentType || null"
    :body="body"
    :load-body-fn="loadBodyFn"
  />

  <BinaryBodyViewer
    v-else-if="body.contentKind === HttpContentKind.Binary"
    :body="body"
    :load-body-fn="loadBodyFn"
  />
</template>